namespace AnemiaScanApi.Infrastructure.Utils.Core;

public interface ICodeGenerator
{
    /// <summary>Случайный код подтверждения из алфавита и длины, заданных в настройках.</summary>
    public string GenerateAlphanumericCode(int? length = null);
}
