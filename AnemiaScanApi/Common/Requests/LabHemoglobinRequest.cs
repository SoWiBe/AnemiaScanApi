using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Гемоглобин из анализа крови, который пользователь вносит к своему скану.
/// </summary>
public class LabHemoglobinRequest
{
    /// <summary>
    /// Г/дл. Многие лаборатории в СНГ печатают г/л (130 вместо 13.0) — границы
    /// специально отсекают такие значения, чтобы они не попали в базу и не
    /// испортили оценку моделей.
    /// </summary>
    [Range(3.0, 25.0, ErrorMessage = "Гемоглобин указывается в г/дл, от 3 до 25 (в г/л разделите на 10: 130 г/л = 13.0 г/дл)")]
    public double Hemoglobin { get; init; }

    /// <summary>
    /// Дата сдачи крови. Не указана — считается сегодняшней.
    /// </summary>
    public DateTime? MeasuredAt { get; init; }
}
