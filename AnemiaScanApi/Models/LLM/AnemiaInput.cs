using System.ComponentModel.DataAnnotations.Schema;

namespace AnemiaScanApi.Models.LLM;

public class AnemiaInput
{
    public IFormFile ImageData { get; set; }
    public string? Label { get; set; } = string.Empty;
    public float? Weight { get; set; } = 1.0f;
    
    [NotMapped]
    public string? ImagePath { get; set; } = string.Empty;
}

