using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Courses;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Services;

public class CourseCatalogService(
    ICoursesRepository coursesRepository,
    ICourseContentRepository courseContentRepository,
    IUsersRepository usersRepository,
    ILogger<CourseCatalogService> logger)
    : BaseService<CourseCatalogService>(logger), ICourseCatalogService
{
    public async Task<IEnumerable<CourseListItemResponse>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var courses = await coursesRepository.GetPublishedAsync(cancellationToken);
        return courses.Select(ToListItem).ToList();
    }

    public async Task<CourseDetailsResponse?> GetDetailsAsync(string slug, CancellationToken cancellationToken = default)
    {
        var course = await coursesRepository.GetBySlugAsync(slug, cancellationToken);
        if (course is null) return null;

        var content = await courseContentRepository.GetByCourseIdAsync(course.Id, cancellationToken);
        var previews = (content?.Days ?? new List<CourseDay>())
            .OrderBy(d => d.DayNumber)
            .Select(d => new CourseDayPreview(d.DayNumber, d.Tasks.Count, d.IsRescanCheckpoint))
            .ToList();

        return new CourseDetailsResponse(
            course.Id,
            course.Slug,
            course.Title,
            course.Description,
            course.TargetAudience,
            course.DurationDays,
            course.IsFree,
            course.PriceUsdc,
            course.PriceKzt,
            course.FreeDaysPreview,
            course.DoctorReviewerName,
            previews);
    }

    public async Task<RecommendedCourseResponse?> GetRecommendedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken);
        var lastScan = user?.AnemiaScans
            .OrderByDescending(s => s.ScanDate)
            .FirstOrDefault();

        if (lastScan is null || !lastScan.IsAnemic)
        {
            return null;
        }

        var courses = (await coursesRepository.GetPublishedAsync(cancellationToken)).ToList();
        if (courses.Count == 0) return null;

        var (audience, reason) = PickAudience(user!);
        var pick = courses.FirstOrDefault(c => c.TargetAudience == audience)
                   ?? courses.FirstOrDefault(c => c.TargetAudience == TargetAudience.Adult);

        return pick is null ? null : new RecommendedCourseResponse(ToListItem(pick), reason);
    }

    private static (TargetAudience audience, string reason) PickAudience(SasUser user)
    {
        // Phase 1 heuristic: no pregnancy/children fields on SasUser yet, fall back to Adult.
        // When SasUser gains those fields (see open questions in the plan doc), extend here.
        _ = user;
        return (TargetAudience.Adult, "По результатам последнего скана");
    }

    private static CourseListItemResponse ToListItem(Course c) => new(
        c.Id,
        c.Slug,
        c.Title,
        c.Description,
        c.TargetAudience,
        c.DurationDays,
        c.IsFree,
        c.PriceUsdc,
        c.PriceKzt,
        c.FreeDaysPreview);
}
