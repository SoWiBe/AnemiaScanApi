using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses;

/// <param name="HemoglobinLevel">
/// CIELab-регрессия Hb (г/дл) — null, если модель не смогла посчитать
/// признаки для этого изображения. Не влияет на <see cref="Sick"/> —
/// это по-прежнему решение основного TF-классификатора.
/// </param>
/// <param name="Severity">Non-Anemic/Mild/Moderate/Severe — null ровно тогда, когда <paramref name="HemoglobinLevel"/> null.</param>
public record AnalyseAnemiaResponse(
    Guid Id,
    double Confidence,
    Sick Sick,
    Guid ImageSystemId,
    DateTime AnalyseDate,
    double? HemoglobinLevel,
    string? Severity);