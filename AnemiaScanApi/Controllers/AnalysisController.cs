using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Models.Responses;
using AnemiaScanApi.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for analyzing anemia.
/// </summary>
/// <param name="logger"></param>
[ApiController]
[Route("[controller]")]
public class AnalysisController(ILogger<AnalysisController> logger, IAnemiaAnalysisService anemiaAnalysisService)
    : BaseSasController(logger)
{
    /// <summary>
    /// Analyze an image for anemia detection
    /// </summary>
    /// <remarks>
    /// Upload an image (conjunctiva, fingernail, palm, etc.) to analyze for anemia indicators.
    /// 
    /// **Supported formats:** JPG, PNG, JPEG
    /// 
    /// **Maximum file size:** 10 MB
    /// 
    /// Sample request:
    /// 
    ///     POST /api/analysis/anemia
    ///     Content-Type: multipart/form-data
    ///     
    ///     image: [binary file data]
    ///     
    /// </remarks>
    /// <param name="request">Anemia analysis request</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Analysis result with anemia prediction and confidence score</returns>
    /// <response code="200">Analysis completed successfully</response>
    /// <response code="400">Invalid image format or file too large</response>
    /// <response code="500">Internal server error during analysis</response>
    [HttpPost("anemia")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AnalyseAnemiaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzeAnemia(
        [FromForm] AnalyseAnemiaRequest request,
        CancellationToken cancellationToken = default)
    {
        //TODO: Create Validation Attribute for this conditions
        if (request.AnemiaImage == null || request.AnemiaImage.Length == 0)
            return BadRequest("No image provided"); //TODO: Create error message constants

        //TODO: Create extension for this operation
        using var memoryStream = new MemoryStream();
        await request.AnemiaImage.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        var (gridFsId, imageId) = await anemiaAnalysisService.SaveImageAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), //TODO: Replace with actual analysis ID, user ID, and image ID
            imageBytes, cancellationToken);

        return Ok(await anemiaAnalysisService.AnalyzeAnemiaAsync(gridFsId, imageId, cancellationToken));
    }

    /// <summary>
    /// Retrieves an image for an analysis.
    /// </summary>
    /// <param name="analyseId">Analysis ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The image data.</returns>
    /// <remarks>
    /// Retrieves the image associated with an analysis.
    /// 
    /// Sample request:
    /// 
    ///     GET /api/analysis/download/{analyseId}
    ///     
    /// </remarks>
    /// <response code="200">Image found</response>
    /// <response code="404">Image not found</response>
    [HttpGet("download-image/{analyseId}")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadImage([FromRoute, Required] string analyseId,
        CancellationToken cancellationToken = default)
    {
        var imageBytes = await anemiaAnalysisService.GetImageAsync(analyseId, cancellationToken);
        return File(imageBytes, "image/jpeg");
    }
}