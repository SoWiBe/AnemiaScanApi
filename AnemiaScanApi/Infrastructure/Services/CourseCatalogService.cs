using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Courses;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Services;

public class CourseCatalogService(
    ICoursesRepository coursesRepository,
    ICourseContentRepository courseContentRepository,
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
            course.DoctorReviewerName,
            previews);
    }

    // Personalized recommendations would reveal the hidden classifier verdict.
    // The general catalog remains available while phone-photo validation is pending.
    public Task<RecommendedCourseResponse?> GetRecommendedAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<RecommendedCourseResponse?>(null);
    private static CourseListItemResponse ToListItem(Course c) => new(
        c.Id,
        c.Slug,
        c.Title,
        c.Description,
        c.TargetAudience,
        c.DurationDays);
}
