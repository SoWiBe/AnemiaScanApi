namespace AnemiaScanApi.Infrastructure.Settings;

public class CodeGeneratorSettings
{
    public string CacheKey { get; set; }
    public string Chars { get; set; }
    public int Length { get; set; } = 6;
}