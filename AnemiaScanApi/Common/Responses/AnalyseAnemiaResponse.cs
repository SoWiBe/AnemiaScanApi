using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses;

public record AnalyseAnemiaResponse(
    Guid Id,
    double Confidence,
    Sick Sick,
    Guid ImageSystemId, 
    DateTime AnalyseDate);