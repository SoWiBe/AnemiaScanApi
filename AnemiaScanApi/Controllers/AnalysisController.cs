using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for analyzing anemia.
/// </summary>
/// <param name="logger"></param>
[ApiController]
[Route("[controller]")]
public class AnalysisController(ILogger<AnalysisController> logger) : ControllerBase
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
        if (request.AnemiaImage == null || request.AnemiaImage.Length == 0)
            return BadRequest("No image provided");

        // TODO: Perform ML analysis and return result
        // How to invoke ML analyse correctly?
        return NoContent();
    }
    
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult RegisterUser([FromBody] string request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request))
            return BadRequest("Email and password are required");
        
        return Ok();
    }
}