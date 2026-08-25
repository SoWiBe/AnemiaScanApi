using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests.Courses;
using AnemiaScanApi.Common.Responses.Courses;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Services.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Controllers;

[Authorize]
[ApiController]
[Route("courses/enrollments")]
public class CourseEnrollmentsController(
    ILogger<CourseEnrollmentsController> logger,
    ICourseEnrollmentService enrollmentService)
    : BaseSasController(logger)
{
    /// <summary>
    /// Мои записи на курсы (любой статус).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EnrollmentSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMineAsync(CancellationToken cancellationToken)
    {
        var enrollments = await enrollmentService.GetMyEnrollmentsAsync(GetUserId(), cancellationToken);
        return Ok(enrollments);
    }

    /// <summary>
    /// Карточка «сегодняшний день» для записи.
    /// </summary>
    [HttpGet("{enrollmentId:guid}/today")]
    [ProducesResponseType(typeof(TodayDayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTodayAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var today = await enrollmentService.GetTodayAsync(GetUserId(), enrollmentId, cancellationToken);
        return today is null ? NoContent() : Ok(today);
    }

    /// <summary>
    /// Отметить задачу выполненной.
    /// </summary>
    [HttpPost("{enrollmentId:guid}/days/{dayNumber:int}/tasks/{taskId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkTaskDoneAsync(Guid enrollmentId, int dayNumber, Guid taskId, CancellationToken cancellationToken)
    {
        await enrollmentService.MarkTaskDoneAsync(GetUserId(), enrollmentId, dayNumber, taskId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Привязать свежий скан к чекпоинт-дню.
    /// </summary>
    [HttpPost("{enrollmentId:guid}/days/{dayNumber:int}/checkpoint-scan")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AttachCheckpointScanAsync(
        Guid enrollmentId,
        int dayNumber,
        [FromBody] AttachCheckpointScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.AnemiaScanId == Guid.Empty)
        {
            throw new SASException(ExceptionMessage.CheckpointScanRequired, 400);
        }

        await enrollmentService.AttachCheckpointScanAsync(GetUserId(), enrollmentId, dayNumber, request.AnemiaScanId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Прогресс по курсу + таймлайн Hb по чекпоинт-сканам.
    /// </summary>
    [HttpGet("{enrollmentId:guid}/progress")]
    [ProducesResponseType(typeof(EnrollmentProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProgressAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var progress = await enrollmentService.GetProgressAsync(GetUserId(), enrollmentId, cancellationToken);
        return Ok(progress);
    }
}
