namespace AnemiaScanApi.Common.Responses;

public record AnalysisHistoryResponse(
    IReadOnlyList<AnalysisHistoryItemResponse> Items,
    long Total,
    int Page,
    int PageSize,
    string Disclaimer);

/// <summary>Public scan metadata and laboratory results; no model predictions.</summary>
public record AnalysisHistoryItemResponse(
    Guid Id,
    DateTime ScanDate,
    Guid? ImageSystemId,
    double? LabHemoglobin,
    DateOnly? LabMeasuredAt)
{
    public string PredictionStatus => "Unavailable";

    public static AnalysisHistoryItemResponse FromScan(AnemiaScan scan) => new(
        scan.Id,
        scan.ScanDate,
        Guid.TryParse(scan.ImageSystemId, out var imageId) ? imageId : null,
        scan.LabHemoglobin,
        scan.LabMeasuredAt is { } measured ? DateOnly.FromDateTime(measured) : null);
}