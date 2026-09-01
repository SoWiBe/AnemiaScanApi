using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests.Payments;
using AnemiaScanApi.Common.Responses.Payments;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class PaymentsController(
    ILogger<PaymentsController> logger,
    IPaymentIntentService paymentIntentService)
    : BaseSasController(logger)
{
    /// <summary>
    /// Создать платёж за курс. Возвращает deep-link кошелька и payload для QR.
    /// Если у пользователя уже есть неистёкший платёж по этому курсу — вернётся он же.
    /// </summary>
    [HttpPost("course-intent")]
    [ProducesResponseType(typeof(PaymentInitiationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCourseIntentAsync(
        [FromBody] CreateCourseIntentRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CourseSlug))
        {
            throw new SASException(ExceptionMessage.CourseNotFound, 404);
        }

        var initiation = await paymentIntentService.CreateCourseIntentAsync(
            GetUserId(), request.CourseSlug, request.Provider, cancellationToken);

        return Ok(initiation);
    }

    /// <summary>
    /// Статус платежа. Фронт поллит этот эндпоинт, пока статус Pending.
    /// При Confirmed в ответе приходит <c>enrollmentId</c> — курс уже открыт.
    /// </summary>
    [HttpGet("intents/{intentId:guid}/status")]
    [ProducesResponseType(typeof(PaymentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatusAsync(Guid intentId, CancellationToken cancellationToken)
    {
        var status = await paymentIntentService.GetStatusAsync(GetUserId(), intentId, cancellationToken);
        return Ok(status);
    }

    /// <summary>
    /// Только для mock-режима (<c>Solana:UseMock = true</c>): принудительно подтвердить платёж,
    /// чтобы не ждать авто-подтверждения. Вне mock-режима отдаёт 404.
    /// </summary>
    [HttpPost("intents/{intentId:guid}/mock-confirm")]
    [ProducesResponseType(typeof(PaymentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MockConfirmAsync(Guid intentId, CancellationToken cancellationToken)
    {
        var status = await paymentIntentService.MockConfirmAsync(GetUserId(), intentId, cancellationToken);
        return Ok(status);
    }
}
