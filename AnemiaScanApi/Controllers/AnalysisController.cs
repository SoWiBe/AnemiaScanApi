using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Common.Requests;
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PredictAnemia([FromForm, Required] PredictionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var prediction = await predictionService.PredictAnemiaAsync(request, cancellationToken);
        var imageBytes = await request.ImageData.UseAsBytesAsync();
        
        var response = await anemiaAnalysisService.WriteAnalyseAsync(
            userId,
            prediction.Score!.Max(),
            prediction.PredictedLabel!,
            imageBytes,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Фоновый анализ анемии
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("anemia/prediction/schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SchedulePredictAnemia([FromForm, Required] PredictionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var prediction = await predictionService.PredictAnemiaAsync(request, cancellationToken);
        var imageBytes = await request.ImageData.UseAsBytesAsync();
        
        var response = await anemiaAnalysisService.WriteAnalyseAsync(
            userId,
            prediction.Score!.Max(),
            prediction.PredictedLabel!,
            imageBytes,
            cancellationToken);

        return Ok(response);
    }
}