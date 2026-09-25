namespace AnemiaScanApi.Settings;

/// <summary>
/// Настройки rate-limit (секция <c>RateLimiting</c>). Значения по умолчанию
/// рассчитаны на MVP-нагрузку; крутить их на проде без передеплоя — через
/// переменные окружения <c>RateLimiting__*</c>.
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Сканов на пользователя за окно. Один скан — это прогон TF-классификатора,
    /// то есть секунды CPU на 2-ядерном VPS; лимит защищает не данные, а процессор.
    /// </summary>
    public int PredictionPermitLimit { get; set; } = 10;

    /// <summary>Длина окна для сканов, минут.</summary>
    public int PredictionWindowMinutes { get; set; } = 1;

    /// <summary>
    /// Писем с кодом на IP за окно. Здесь лимит защищает репутацию SMTP-ящика:
    /// открытый эндпоинт рассылки — это чужой спам с нашего адреса.
    /// </summary>
    public int EmailCodePermitLimit { get; set; } = 5;

    /// <summary>Длина окна для писем с кодом, минут.</summary>
    public int EmailCodeWindowMinutes { get; set; } = 10;
}
