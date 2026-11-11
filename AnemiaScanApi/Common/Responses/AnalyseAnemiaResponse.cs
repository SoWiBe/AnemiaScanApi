namespace AnemiaScanApi.Common.Responses;

/// <summary>Receipt for a stored scan. Model predictions remain internal until phone validation.</summary>
public record AnalyseAnemiaResponse(Guid Id, Guid ImageSystemId, DateTime AnalyseDate, string Disclaimer)
{
    public string PredictionStatus => "Unavailable";
}