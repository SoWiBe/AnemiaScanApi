using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Models.LLM;

public class AnemiaInput
{
    public string Label { get; set; } = string.Empty;
    public float Weight { get; set; } = 1.0f;
    public string ImagePath { get; set; } = string.Empty;
}