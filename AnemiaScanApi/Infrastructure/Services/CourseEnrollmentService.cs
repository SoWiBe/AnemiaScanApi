using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Courses;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Services;

public class CourseEnrollmentService(
    ICoursesRepository coursesRepository,
    ICourseContentRepository courseContentRepository,
    ICourseEnrollmentsRepository enrollmentsRepository,
    IAnemiaScansRepository anemiaScansRepository,
    IStreakService streakService,
    ICourseEntitlementService entitlementService,
    ILogger<CourseEnrollmentService> logger)
    : BaseService<CourseEnrollmentService>(logger), ICourseEnrollmentService
{
    public async Task<EnrollResponse> EnrollAsync(Guid userId, string slug, CancellationToken cancellationToken = default)
    {
        var course = await coursesRepository.GetBySlugAsync(slug, cancellationToken)
            ?? throw new SASException(ExceptionMessage.CourseNotFound, 404);

        var existing = await enrollmentsRepository.GetByUserAndCourseAsync(userId, course.Id, cancellationToken);
        if (existing is not null)
        {
            throw new SASException(ExceptionMessage.AlreadyEnrolled, 409);
        }

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };

        await enrollmentsRepository.CreateAsync(enrollment, cancellationToken);
        Logger.LogInformation("User {UserId} enrolled in course {CourseSlug} ({EnrollmentId})", userId, course.Slug, enrollment.Id);

        return new EnrollResponse(enrollment.Id, course.Id, course.Slug);
    }

    public async Task<IEnumerable<EnrollmentSummaryResponse>> GetMyEnrollmentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var enrollments = (await enrollmentsRepository.GetByUserIdAsync(userId, cancellationToken)).ToList();
        if (enrollments.Count == 0) return Array.Empty<EnrollmentSummaryResponse>();

        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var courseById = new Dictionary<Guid, Course>();
        foreach (var id in courseIds)
        {
            var c = await coursesRepository.GetByIdAsync(id, cancellationToken);
            if (c is not null) courseById[id] = c;
        }

        var utcNow = DateTime.UtcNow;
        return enrollments
            .Where(e => courseById.ContainsKey(e.CourseId))
            .Select(e =>
            {
                var course = courseById[e.CourseId];
                return new EnrollmentSummaryResponse(
                    e.Id,
                    course.Id,
                    course.Slug,
                    course.Title,
                    course.DurationDays,
                    e.Status,
                    ComputeCurrentDayNumber(e, course.DurationDays, utcNow),
                    e.Days.Count(d => d.CompletedAt is not null),
                    e.CurrentStreak,
                    e.LongestStreak,
                    e.EnrolledAt);
            })
            .ToList();
    }

    public async Task<TodayDayResponse?> GetTodayAsync(Guid userId, Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await LoadOwnedEnrollmentAsync(userId, enrollmentId, cancellationToken);
        var course = await LoadCourseAsync(enrollment.CourseId, cancellationToken);
        var content = await LoadContentAsync(course.Id, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var dayNumber = ComputeCurrentDayNumber(enrollment, course.DurationDays, utcNow);
        var day = content.Days.FirstOrDefault(d => d.DayNumber == dayNumber);
        if (day is null) return null;

        await entitlementService.EnsureDayAccessAsync(course, enrollment, dayNumber, cancellationToken);

        var completion = enrollment.Days.FirstOrDefault(d => d.DayNumber == dayNumber);
        var completedIds = completion?.CompletedTaskIds ?? new List<Guid>();

        var tasks = day.Tasks
            .Select(t => new TodayTask(t.Id, t.Title, t.Description, completedIds.Contains(t.Id)))
            .ToList();

        return new TodayDayResponse(
            enrollment.Id,
            course.Id,
            dayNumber,
            day.Theory,
            day.IsRescanCheckpoint,
            completion?.CheckpointScanId,
            tasks,
            completion?.CompletedAt is not null);
    }

    public async Task MarkTaskDoneAsync(Guid userId, Guid enrollmentId, int dayNumber, Guid taskId, CancellationToken cancellationToken = default)
    {
        var enrollment = await LoadOwnedEnrollmentAsync(userId, enrollmentId, cancellationToken);
        var course = await LoadCourseAsync(enrollment.CourseId, cancellationToken);
        var content = await LoadContentAsync(course.Id, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var currentDay = ComputeCurrentDayNumber(enrollment, course.DurationDays, utcNow);
        if (dayNumber > currentDay)
        {
            throw new SASException(ExceptionMessage.DayNotYetAvailable, 409);
        }

        var day = content.Days.FirstOrDefault(d => d.DayNumber == dayNumber)
            ?? throw new SASException(ExceptionMessage.CourseDayNotFound, 404);

        await entitlementService.EnsureDayAccessAsync(course, enrollment, dayNumber, cancellationToken);

        if (day.Tasks.All(t => t.Id != taskId))
        {
            throw new SASException(ExceptionMessage.CourseTaskNotFound, 404);
        }

        var completion = enrollment.Days.FirstOrDefault(d => d.DayNumber == dayNumber);
        if (completion is null)
        {
            completion = new DayCompletion { DayNumber = dayNumber };
            enrollment.Days.Add(completion);
        }

        if (!completion.CompletedTaskIds.Contains(taskId))
        {
            completion.CompletedTaskIds.Add(taskId);
        }

        streakService.ApplyDayActivity(enrollment, utcNow);
        TryMarkDayCompleted(day, completion, utcNow);
        TryMarkEnrollmentCompleted(enrollment, course.DurationDays, utcNow);

        await enrollmentsRepository.UpdateAsync(enrollment.Id, enrollment, cancellationToken);
    }

    public async Task AttachCheckpointScanAsync(Guid userId, Guid enrollmentId, int dayNumber, Guid anemiaScanId, CancellationToken cancellationToken = default)
    {
        var enrollment = await LoadOwnedEnrollmentAsync(userId, enrollmentId, cancellationToken);
        var course = await LoadCourseAsync(enrollment.CourseId, cancellationToken);
        var content = await LoadContentAsync(course.Id, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var day = content.Days.FirstOrDefault(d => d.DayNumber == dayNumber)
            ?? throw new SASException(ExceptionMessage.CourseDayNotFound, 404);

        if (!day.IsRescanCheckpoint)
        {
            throw new SASException(ExceptionMessage.CheckpointScanRequired, 400);
        }

        await entitlementService.EnsureDayAccessAsync(course, enrollment, dayNumber, cancellationToken);

        var scan = await anemiaScansRepository.GetAnemiaScanAsync(anemiaScanId.ToString(), cancellationToken);
        if (scan is null || scan.UserId != userId.ToString())
        {
            throw new SASException(ExceptionMessage.ProfileNotFound, 404);
        }

        var completion = enrollment.Days.FirstOrDefault(d => d.DayNumber == dayNumber);
        if (completion is null)
        {
            completion = new DayCompletion { DayNumber = dayNumber };
            enrollment.Days.Add(completion);
        }

        completion.CheckpointScanId = anemiaScanId;

        streakService.ApplyDayActivity(enrollment, utcNow);
        TryMarkDayCompleted(day, completion, utcNow);
        TryMarkEnrollmentCompleted(enrollment, course.DurationDays, utcNow);

        await enrollmentsRepository.UpdateAsync(enrollment.Id, enrollment, cancellationToken);
    }

    public async Task<EnrollmentProgressResponse> GetProgressAsync(Guid userId, Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await LoadOwnedEnrollmentAsync(userId, enrollmentId, cancellationToken);
        var course = await LoadCourseAsync(enrollment.CourseId, cancellationToken);
        var content = await LoadContentAsync(course.Id, cancellationToken);

        var completionsByDay = enrollment.Days.ToDictionary(d => d.DayNumber);
        var days = content.Days
            .OrderBy(d => d.DayNumber)
            .Select(d =>
            {
                completionsByDay.TryGetValue(d.DayNumber, out var completion);
                return new DayProgressItem(
                    d.DayNumber,
                    d.IsRescanCheckpoint,
                    d.Tasks.Count,
                    completion?.CompletedTaskIds.Count ?? 0,
                    completion?.CompletedAt is not null,
                    completion?.CompletedAt,
                    completion?.CheckpointScanId);
            })
            .ToList();

        var timeline = new List<HemoglobinPoint>();
        foreach (var completion in enrollment.Days.Where(d => d.CheckpointScanId is not null))
        {
            var scan = await anemiaScansRepository.GetAnemiaScanAsync(completion.CheckpointScanId!.Value.ToString(), cancellationToken);
            if (scan is null) continue;
            timeline.Add(new HemoglobinPoint(completion.DayNumber, scan.ScanDate, scan.HemoglobinLevel, scan.IsAnemic));
        }

        return new EnrollmentProgressResponse(
            enrollment.Id,
            course.Id,
            course.Title,
            course.DurationDays,
            enrollment.Status,
            enrollment.CurrentStreak,
            enrollment.LongestStreak,
            enrollment.EnrolledAt,
            enrollment.CompletedAt,
            days,
            timeline.OrderBy(p => p.DayNumber).ToList());
    }

    private async Task<CourseEnrollment> LoadOwnedEnrollmentAsync(Guid userId, Guid enrollmentId, CancellationToken cancellationToken)
    {
        var enrollment = await enrollmentsRepository.GetByIdAsync(enrollmentId, cancellationToken);
        if (enrollment is null || enrollment.UserId != userId)
        {
            throw new SASException(ExceptionMessage.EnrollmentNotFound, 404);
        }
        return enrollment;
    }

    private async Task<Course> LoadCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var course = await coursesRepository.GetByIdAsync(courseId, cancellationToken);
        if (course is null)
        {
            throw new SASException(ExceptionMessage.CourseNotFound, 404);
        }
        return course;
    }

    private async Task<CourseContent> LoadContentAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var content = await courseContentRepository.GetByCourseIdAsync(courseId, cancellationToken);
        if (content is null)
        {
            throw new SASException(ExceptionMessage.CourseContentNotFound, 404);
        }
        return content;
    }

    private static int ComputeCurrentDayNumber(CourseEnrollment enrollment, int durationDays, DateTime utcNow)
    {
        var daysElapsed = (int)(utcNow.Date - enrollment.EnrolledAt.Date).TotalDays;
        var current = daysElapsed + 1;
        if (current < 1) current = 1;
        if (current > durationDays) current = durationDays;
        return current;
    }

    private static void TryMarkDayCompleted(CourseDay day, DayCompletion completion, DateTime utcNow)
    {
        if (completion.CompletedAt is not null) return;

        var allTasksDone = day.Tasks.All(t => completion.CompletedTaskIds.Contains(t.Id));
        var checkpointOk = !day.IsRescanCheckpoint || completion.CheckpointScanId is not null;

        if (allTasksDone && checkpointOk)
        {
            completion.CompletedAt = utcNow;
        }
    }

    private static void TryMarkEnrollmentCompleted(CourseEnrollment enrollment, int durationDays, DateTime utcNow)
    {
        if (enrollment.Status == EnrollmentStatus.Completed) return;

        var completedDays = enrollment.Days.Count(d => d.CompletedAt is not null);
        if (completedDays >= durationDays)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.CompletedAt = utcNow;
        }
    }
}
