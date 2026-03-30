namespace AnemiaScanApi.Infrastructure.Utils.Core;

public interface ICodeGenerator
{
    public (string, string) GenerateAlphanumericCode(string email, int? length = null);
    public string GetCacheKey(string email);
}