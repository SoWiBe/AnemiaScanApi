namespace AnemiaScanApi.Models.LLM;

public class AnemiaPredictionOutput : AnemiaInput
{
    public float[]? Score { get; set; }
    public string? PredictedLabel { get; set; }
}