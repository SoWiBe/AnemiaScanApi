using AnemiaScanApi.Common.Responses.Courses;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Reads the course catalog and produces a per-user recommendation.
/// </summary>
public interface ICourseCatalogService
{
    /// <summary>
    /// Returns all published courses in the catalog.
    /// </summary>
    Task<IEnumerable<CourseListItemResponse>> GetCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full details of a published course (or null if not found).
    /// </summary>
    Task<CourseDetailsResponse?> GetDetailsAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a recommendation based on the user's latest scan and profile.
    /// Null if no relevant course applies (no anemic scan, or catalog empty).
    /// </summary>
    Task<RecommendedCourseResponse?> GetRecommendedAsync(Guid userId, CancellationToken cancellationToken = default);
}
