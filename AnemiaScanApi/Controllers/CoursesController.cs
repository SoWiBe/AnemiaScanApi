using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Responses.Courses;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Services.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class CoursesController(
    ILogger<CoursesController> logger,
    ICourseCatalogService catalogService,
    ICourseEnrollmentService enrollmentService)
    : BaseSasController(logger)
{
    /// <summary>
    /// Список опубликованных курсов.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CourseListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var items = await catalogService.GetCatalogAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Рекомендованный курс на основе последнего скана и профиля пользователя.
    /// </summary>
    [HttpGet("recommended")]
    [ProducesResponseType(typeof(RecommendedCourseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendedAsync(CancellationToken cancellationToken)
    {
        var recommended = await catalogService.GetRecommendedAsync(GetUserId(), cancellationToken);
        return recommended is null ? NoContent() : Ok(recommended);
    }

    /// <summary>
    /// Детали конкретного курса.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CourseDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDetailsAsync(string slug, CancellationToken cancellationToken)
    {
        var details = await catalogService.GetDetailsAsync(slug, cancellationToken);
        if (details is null)
        {
            throw new SASException(ExceptionMessage.CourseNotFound, 404);
        }
        return Ok(details);
    }

    /// <summary>
    /// Записаться на курс.
    /// </summary>
    [HttpPost("{slug}/enroll")]
    [ProducesResponseType(typeof(EnrollResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnrollAsync(string slug, CancellationToken cancellationToken)
    {
        var response = await enrollmentService.EnrollAsync(GetUserId(), slug, cancellationToken);
        return Ok(response);
    }
}
