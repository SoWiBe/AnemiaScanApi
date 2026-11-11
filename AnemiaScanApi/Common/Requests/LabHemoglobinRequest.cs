namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Гемоглобин из анализа крови, который пользователь вносит к своему скану.
///
/// Атрибутов валидации здесь намеренно нет: проверки живут в сервисе
/// (AnemiaAnalysisService.SetLabHemoglobinAsync), чтобы все отказы, которые
/// видит пользователь, приходили в одном виде — <c>{ statusCode, description }</c>.
/// С атрибутами ошибка диапазона уходила в другом формате, чем ошибка даты.
/// </summary>
public class LabHemoglobinRequest
{
    /// <summary>
    /// Г/дл, от 3 до 25. Nullable, чтобы отличить «поле не прислали» от нуля.
    /// </summary>
    public double? Hemoglobin { get; init; }

    /// <summary>
    /// Дата сдачи крови — календарная, <c>ГГГГ-ММ-ДД</c>, без времени и пояса.
    ///
    /// Именно DateOnly, а не DateTime: дата анализа — это день по календарю
    /// пользователя, а DateTime проходил через часовой пояс сервера и мог
    /// сохраниться соседним днём. Не указана — берётся сегодняшняя по UTC.
    /// </summary>
    public DateOnly? MeasuredAt { get; init; }
}
