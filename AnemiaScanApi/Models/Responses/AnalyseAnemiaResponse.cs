namespace AnemiaScanApi.Models.Responses;

/// <summary>
/// Response model for analyzing anemia.
/// </summary>
public record AnalyseAnemiaResponse
{
    /// <summary>
    /// Analysis ID.
    /// </summary>
    public Guid AnalysisId { get; init; }
    /// <summary>
    /// Confidence level of the analysis result.
    /// </summary>
    public double Confidence { get; init; }
    /// <summary>
    /// Image system ID associated with the analysis.
    /// </summary>
    public Guid ImageSystemId { get; init; }
}