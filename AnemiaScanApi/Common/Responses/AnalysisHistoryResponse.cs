using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.ML;

namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Страница истории сканов пользователя (P0 №8 в docs/plans/MVP_PLAN.md).
/// До этого эндпоинта мобилке приходилось тянуть всю историю через
/// <c>GET /profile/info</c> вместе со всем документом пользователя.
/// </summary>
/// <param name="Disclaimer">
/// Медицинский дисклеймер (P0 №12) — один на ответ, а не на каждый элемент:
/// экран истории показывает вердикты, значит должен показать и оговорку.
/// </param>
public record AnalysisHistoryResponse(
    IReadOnlyList<AnalysisHistoryItemResponse> Items,
    long Total,
    int Page,
    int PageSize,
    string Disclaimer);

/// <summary>
/// Один скан в списке истории. Набор полей совпадает с
/// <see cref="AnalyseAnemiaResponse"/>, чтобы экран результата и экран истории
/// рисовали карточку одним и тем же кодом.
/// </summary>
/// <param name="HemoglobinLevel">CIELab-регрессия Hb (г/дл); null, если модель не смогла посчитать признаки.</param>
/// <param name="Severity">Non-Anemic/Mild/Moderate/Severe — null ровно тогда, когда <paramref name="HemoglobinLevel"/> null.</param>
/// <param name="ImageSystemId">
/// Идентификатор снимка. Null у записей, где он не разбирается как Guid —
/// в базе это строка, и ломать выдачу истории из-за одной кривой записи незачем.
/// </param>
public record AnalysisHistoryItemResponse(
    Guid Id,
    DateTime ScanDate,
    Sick Sick,
    double Confidence,
    double? HemoglobinLevel,
    string? Severity,
    SeverityReference? SeverityReference,
    VerdictAgreement? VerdictAgreement,
    Guid? ImageSystemId);
