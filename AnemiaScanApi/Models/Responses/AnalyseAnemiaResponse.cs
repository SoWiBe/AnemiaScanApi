using AnemiaScanApi.Models.Enums;

namespace AnemiaScanApi.Models.Responses;

public record AnalyseAnemiaResponse(
    Guid Id,
    double Confidence,
    Sick Sick,
    Guid ImageSystemId, 
    DateTime AnalyseDate);