using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.ML;

namespace AnemiaScanApi.Common.Responses;

/// <param name="HemoglobinLevel">
/// CIELab-регрессия Hb (г/дл) — null, если модель не смогла посчитать
/// признаки для этого изображения. Не влияет на <see cref="Sick"/> —
/// это по-прежнему решение основного TF-классификатора.
/// </param>
/// <param name="Severity">Non-Anemic/Mild/Moderate/Severe — null ровно тогда, когда <paramref name="HemoglobinLevel"/> null.</param>
/// <param name="SeverityReference">
/// Шкала ВОЗ, по которой получена <paramref name="Severity"/> (P0 №10):
/// у взрослого мужчины и у ребёнка границы разные, и клиенту стоит показывать,
/// к какой группе отнесён пациент.
/// </param>
/// <param name="DemographicsComplete">
/// false — в профиле нет пола или возраста, оценка сделана по самой строгой
/// шкале. Повод попросить пользователя дозаполнить профиль.
/// </param>
/// <param name="Verdict">
/// Единый вердикт по снимку (P0 №11): исход, согласие двух моделей и признак
/// того, получено ли число Hb моделью для популяции этого пациента.
/// </param>
/// <param name="Disclaimer">
/// Медицинский дисклеймер (P0 №12). Едет в каждом ответе, чтобы экран
/// результата физически не мог показать вердикт без него.
/// </param>
public record AnalyseAnemiaResponse(
    Guid Id,
    double Confidence,
    Sick Sick,
    Guid ImageSystemId,
    DateTime AnalyseDate,
    double? HemoglobinLevel,
    string? Severity,
    SeverityReference? SeverityReference,
    bool DemographicsComplete,
    AnalysisVerdict Verdict,
    string Disclaimer);
