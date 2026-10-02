using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Параметры постраничного запроса истории сканов (P0 №8).
/// </summary>
public class AnalysisHistoryRequest
{
    /// <summary>
    /// Номер страницы, начиная с 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Номер страницы должен быть не меньше 1")]
    public int Page { get; init; } = 1;

    /// <summary>
    /// Размер страницы. Потолок в 100 — чтобы один запрос не вытягивал всю историю
    /// пользователя и не превращался в способ нагрузить базу.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Размер страницы должен быть от 1 до 100")]
    public int PageSize { get; init; } = 20;
}
