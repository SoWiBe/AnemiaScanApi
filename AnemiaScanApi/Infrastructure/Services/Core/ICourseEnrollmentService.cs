using AnemiaScanApi.Common.Responses.Courses;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Manages a user's enrollments in courses and daily progress.
/// </summary>
public interface ICourseEnrollmentService
{
    /// <summary>
    /// Enrolls the user in a course by slug. Throws if already enrolled.
    /// </summary>
    Task<EnrollResponse> EnrollAsync(Guid userId, string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all enrollments (any status) for the user.
    /// </summary>
    Task<IEnumerable<EnrollmentSummaryResponse>> GetMyEnrollmentsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the "today" card for the given enrollment based on days elapsed since <c>EnrolledAt</c>.
    /// Null if the enrollment has already been completed or the day is out of range.
    /// </summary>
    Task<TodayDayResponse?> GetTodayAsync(Guid userId, Guid enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a task done. Idempotent — marking an already-done task is a no-op.
    /// Updates streak and, if this was the last task of the day, sets the day's <c>CompletedAt</c>.
    /// </summary>
    Task MarkTaskDoneAsync(Guid userId, Guid enrollmentId, int dayNumber, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a fresh <see cref="AnemiaScan"/> to a rescan-checkpoint day.
    /// </summary>
    Task AttachCheckpointScanAsync(Guid userId, Guid enrollmentId, int dayNumber, Guid anemiaScanId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-day progress + Hb timeline from checkpoint scans.
    /// </summary>
    Task<EnrollmentProgressResponse> GetProgressAsync(Guid userId, Guid enrollmentId, CancellationToken cancellationToken = default);
}
