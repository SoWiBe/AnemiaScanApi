namespace AnemiaScanApi.Common.Requests.Profile;

/// <summary>
/// Принятие действующей версии политики конфиденциальности (P0 №13).
/// </summary>
public class AcceptConsentRequest
{
    /// <summary>
    /// Версия политики, которую показали пользователю. Пусто — считаем, что
    /// показали действующую; расхождение с действующей — 400.
    /// </summary>
    public string? PolicyVersion { get; init; }
}
