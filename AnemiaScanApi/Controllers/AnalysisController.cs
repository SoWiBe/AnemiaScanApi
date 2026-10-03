using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Extensions;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class AnalysisController(
    ILogger<AnalysisController> logger,
    IAnemiaAnalysisService anemiaAnalysisService,
    IPredictionService predictionService)
    : BaseSasController(logger)
{
    /// <summary>
    /// Анализ анемии
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("anemia/prediction/")]
    [EnableRateLimiting(RateLimitPolicies.AnemiaPrediction)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PredictAnemia([FromForm, Required] PredictionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var prediction = await predictionService.PredictAnemiaAsync(request, cancellationToken);
        var imageBytes = await request.ImageData.UseAsBytesAsync();
        // CIELab-регрессия Hb вызывается внутри WriteAnalyseAsync: какую из двух
        // моделей брать, решает возраст, а он известен только после чтения профиля.

        var response = await anemiaAnalysisService.WriteAnalyseAsync(
            userId,
            prediction,
            imageBytes,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// История сканов пользователя, новые сверху (P0 №8).
    /// Нужна экрану истории: до этого список доставался только целиком
    /// через GET /profile/info вместе со всем документом пользователя.
    /// </summary>
    [HttpGet("history/")]
    [ProducesResponseType(typeof(AnalysisHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory([FromQuery] AnalysisHistoryRequest request, CancellationToken cancellationToken)
    {
        var response = await anemiaAnalysisService.GetHistoryAsync(
            GetUserId(), request.Page, request.PageSize, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Фоновый анализ анемии
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("anemia/prediction/schedule")]
    [EnableRateLimiting(RateLimitPolicies.AnemiaPrediction)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SchedulePredictAnemia([FromForm, Required] PredictionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var prediction = await predictionService.PredictAnemiaAsync(request, cancellationToken);
        var imageBytes = await request.ImageData.UseAsBytesAsync();

        var response = await anemiaAnalysisService.WriteAnalyseAsync(
            userId,
            prediction,
            imageBytes,
            cancellationToken);

        return Ok(response);
    }
}