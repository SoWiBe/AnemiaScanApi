using AnemiaScanApi.Common;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Decides whether a user may open a given day of a course.
///
/// Lives as a service rather than an action filter because the decision needs the course and the
/// enrollment already loaded — a filter would have to re-read both from Mongo on every request.
/// </summary>
public interface ICourseEntitlementService
{
    /// <summary>
    /// Throws <c>SASException(402)</c> unless the day is reachable: the course is free, the day
    /// falls inside the free preview window, or the enrollment is backed by a confirmed payment.
    /// </summary>
    Task EnsureDayAccessAsync(Course course, CourseEnrollment enrollment, int dayNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-throwing check for whether the enrollment has access beyond the free preview.
    /// </summary>
    Task<bool> HasPaidAccessAsync(CourseEnrollment enrollment, CancellationToken cancellationToken = default);
}
