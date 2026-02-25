using AnemiaScanApi.Infrastructure.Settings;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Infrastructure.Utils;

/// <summary>
/// Сервис генирации кода-верификации
/// </summary>
public class CodeGenerator(IOptions<CodeGeneratorSettings> codeGeneratorSettings) : ICodeGenerator
{
    private readonly Random _random = new();

    /// <summary>
    /// Генерация буквенно-цифрового кода
    /// </summary>
    /// <param name="email"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public (string, string) GenerateAlphanumericCode(string email, int? length = null)
    {
        var chars = codeGeneratorSettings.Value.Chars;
        
        var code = new string(Enumerable.Repeat(chars, length ?? codeGeneratorSettings.Value.Length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
        
        return ($"{codeGeneratorSettings.Value.CacheKey}:{email.ToLower()}", code);
    }
}