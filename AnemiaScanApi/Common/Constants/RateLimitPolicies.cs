namespace AnemiaScanApi.Common.Constants;

/// <summary>
/// Имена политик rate-limit. Навешиваются на действия через
/// <c>[EnableRateLimiting]</c>, регистрируются в
/// <see cref="Extensions.RateLimitingExtensions"/>.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Тяжёлый CPU-эндпоинт анализа снимка. Партиция — пользователь.</summary>
    public const string AnemiaPrediction = "anemia-prediction";

    /// <summary>Отправка кода на почту. Партиция — IP (тело запроса на этом этапе недоступно).</summary>
    public const string EmailCodes = "email-codes";
}
