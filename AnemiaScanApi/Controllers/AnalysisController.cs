using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Extensions;
using AnemiaScanApi.Infrastructure.Services.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [HttpPost("anemia/prediction")]
    public async Task<IActionResult> PredictAnemia([FromForm, Required] PredictionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var prediction = await predictionService.PredictAnemiaAsync(request, cancellationToken);
        var imageBytes = await request.ImageData.UseAsBytesAsync();
        
        // записываем результаты
        var response = await anemiaAnalysisService.WriteAnalyseAsync(
            userId, 
            prediction.Score!.Max(), 
            prediction.PredictedLabel!, 
            imageBytes, 
            cancellationToken);

        return Ok(response);
    }
}