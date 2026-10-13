using System.Security.Cryptography;

using AnemiaScanApi.Infrastructure.Settings;
using AnemiaScanApi.Infrastructure.Utils.Core;
using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Infrastructure.Utils;

/// <summary>
/// Сервис генирации кода-верификации
/// </summary>
public class CodeGenerator(IOptions<CodeGeneratorSettings> codeGeneratorSettings) : ICodeGenerator
{
    /// <summary>
    /// Генерация буквенно-цифрового кода.
    ///
    /// Криптографический генератор, а не <c>System.Random</c>: этим кодом входят в
    /// аккаунт и сбрасывают пароль, а Random предсказуем по нескольким выходам.
    /// </summary>
    public string GenerateAlphanumericCode(int? length = null)
    {
        var chars = codeGeneratorSettings.Value.Chars;
        return RandomNumberGenerator.GetString(chars, length ?? codeGeneratorSettings.Value.Length);
    }
}
