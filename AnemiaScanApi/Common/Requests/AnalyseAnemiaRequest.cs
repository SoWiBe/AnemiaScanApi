using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request model for analyzing anemia.
/// </summary>
public class AnalyseAnemiaRequest
{
    /// <summary>
    /// Anemia image file.
    /// </summary>
    [FromForm, Required] public IFormFile AnemiaImage { get; set; }
}