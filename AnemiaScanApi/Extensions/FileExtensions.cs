namespace AnemiaScanApi.Extensions;

public static class FileExtensions
{
    public static async Task<byte[]> UseAsBytesAsync(this IFormFile formFile)
    {
        using var memoryStream = new MemoryStream();
        await formFile.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}