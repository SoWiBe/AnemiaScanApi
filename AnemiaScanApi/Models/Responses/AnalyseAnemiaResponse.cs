namespace AnemiaScanApi.Models.Responses;

/// <summary>
/// Response model for analyzing anemia.
/// </summary>
public class AnalyseAnemiaResponse
{
    /// <summary>
    /// Confidence level of the analysis result.
    /// </summary>
    public double Confidence { get; set; }
}