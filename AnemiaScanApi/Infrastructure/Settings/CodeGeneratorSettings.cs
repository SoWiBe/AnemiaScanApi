namespace AnemiaScanApi.Infrastructure.Settings;

public class CodeGeneratorSettings
{
    /// <summary>
    /// Не используется с 12.10.2026: был префиксом ключа в IMemoryCache, а коды
    /// теперь лежат в Mongo (VerificationCodeStore). Свойство оставлено, потому
    /// что переменная CodeGenerator__CacheKey всё ещё приезжает из деплоя.
    /// </summary>
    public string? CacheKey { get; set; }
    public string Chars { get; set; } = null!;
    public int Length { get; set; } = 6;
}
