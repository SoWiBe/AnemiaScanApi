namespace AnemiaScanApi.Settings;

/// <summary>
/// Юридические тексты и версия политики конфиденциальности (секция <c>Legal</c>).
/// Версия — ключевое поле: именно она пишется в согласие пользователя (P0 №13),
/// и по её расхождению с принятой клиент понимает, что нужно переподписать.
/// </summary>
public class LegalSettings
{
    /// <summary>Версия, на которую откатываемся, если конфиг пустой.</summary>
    public const string DefaultPolicyVersion = "1.0";

    /// <summary>
    /// Действующая версия политики конфиденциальности. Поднимать при любом
    /// изменении текста политики, иначе старые согласия будут выглядеть
    /// актуальными.
    /// </summary>
    public string PolicyVersion { get; set; } = DefaultPolicyVersion;

    /// <summary>Ссылка на опубликованный текст политики. Заполняется на деплое.</summary>
    public string? PolicyUrl { get; set; }

    /// <summary>
    /// Переопределение текста дисклеймера. Пусто — берётся
    /// <see cref="Common.Constants.MedicalDisclaimer.Text"/>.
    /// </summary>
    public string? DisclaimerText { get; set; }
}
