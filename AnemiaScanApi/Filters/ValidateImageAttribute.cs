using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;

namespace AnemiaScanApi.Filters;

/// <summary>Validates the nested ImageData upload before either prediction action runs.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateImageAttribute : ActionFilterAttribute
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 64 * 1024;
    public const long MaxPixels = 16_000_000;
    public const int MaxDimension = 8192;

    private static readonly Configuration ImageConfiguration = CreateConfiguration();
    private static readonly DecoderOptions DecodeOptions = new()
    {
        Configuration = ImageConfiguration,
        SkipMetadata = true,
        MaxFrames = 2
    };

    private static Configuration CreateConfiguration()
    {
        var configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            MaximumPoolSizeMegabytes = 32,
            AllocationLimitMegabytes = 128
        });
        return configuration;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var file = context.ActionArguments.Values.OfType<PredictionRequest>().FirstOrDefault()?.ImageData;
        if (file is null || file.Length == 0)
        {
            Reject(context, "Загрузите непустое изображение");
            return;
        }
        if (file.Length > MaxFileBytes)
        {
            Reject(context, "Размер изображения не должен превышать 10 МиБ", HttpStatusCode.RequestEntityTooLarge);
            return;
        }

        var expectedFormat = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "JPEG",
            ".png" => "PNG",
            ".bmp" => "BMP",
            ".gif" => "GIF",
            ".webp" => "WEBP",
            _ => null
        };
        if (expectedFormat is null)
        {
            Reject(context, "Допустимые форматы: JPEG, PNG, BMP, GIF, WebP");
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var cancellationToken = context.HttpContext.RequestAborted;
            var info = await Image.IdentifyAsync(DecodeOptions, stream, cancellationToken);
            var format = info.Metadata.DecodedImageFormat;
            var contentType = file.ContentType;
            var mimeMatches = format is not null && (format.MimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
                || (format.Name == "JPEG" && string.Equals(contentType, "image/jpg", StringComparison.OrdinalIgnoreCase)));
            if (!string.Equals(format?.Name, expectedFormat, StringComparison.OrdinalIgnoreCase) || !mimeMatches)
            {
                Reject(context, "Содержимое изображения не соответствует расширению или Content-Type");
                return;
            }
            if (info.Width > MaxDimension || info.Height > MaxDimension || (long)info.Width * info.Height > MaxPixels)
            {
                Reject(context, "Изображение должно быть не более 16 мегапикселей и 8192 пикселей по каждой стороне");
                return;
            }
            if (info.FrameMetadataCollection.Count > 1)
            {
                Reject(context, "Загрузите неподвижное изображение без анимации");
                return;
            }

            // Header inspection alone cannot detect a truncated or corrupt pixel payload.
            // Reopen rather than seek: uploaded streams are not required to support seeking.
            await using var decodeStream = file.OpenReadStream();
            using var decoded = await Image.LoadAsync<Rgba32>(DecodeOptions, decodeStream, cancellationToken);
            if (decoded.Frames.Count > 1)
            {
                Reject(context, "Загрузите неподвижное изображение без анимации");
                return;
            }
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException
            or NotSupportedException or InvalidMemoryOperationException)
        {
            Reject(context, "Файл повреждён или не является поддерживаемым изображением");
            return;
        }

        await next();
    }

    private static void Reject(ActionExecutingContext context, string description,
        HttpStatusCode status = HttpStatusCode.BadRequest)
        => context.Result = new ObjectResult(new ExceptionResponse(status, description)) { StatusCode = (int)status };
}
