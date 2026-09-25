using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AnemiaScanApi.Utils;

/// <summary>
/// Downscale + перекодирование в JPEG перед сохранением в GridFS (P0 №9).
///
/// Применяется только к копии, которая уходит в базу: и TF-классификатор, и
/// CIELab-регрессия считаются в контроллере по оригинальным байтам загрузки,
/// до того как сюда что-то попадёт. Менять этот порядок нельзя — CIELab-признаки
/// чувствительны к артефактам JPEG-сжатия.
/// </summary>
public class ImageCompressor(IOptions<ImageStorageSettings> settings, ILogger<ImageCompressor> logger) : IImageCompressor
{
    public byte[] CompressForStorage(byte[] image)
    {
        if (image.Length == 0) return image;

        try
        {
            using var loaded = Image.Load(image);

            var maxDimension = settings.Value.MaxDimension;
            if (loaded.Width > maxDimension || loaded.Height > maxDimension)
            {
                loaded.Mutate(x => x.Resize(new ResizeOptions
                {
                    // Max сохраняет пропорции и вписывает в квадрат maxDimension.
                    Mode = ResizeMode.Max,
                    Size = new Size(maxDimension, maxDimension)
                }));
            }

            using var output = new MemoryStream();
            loaded.Save(output, new JpegEncoder { Quality = settings.Value.JpegQuality });
            var compressed = output.ToArray();

            // Уже сжатый маленький JPEG после перекодирования может стать больше —
            // в этом случае честнее оставить оригинал.
            if (compressed.Length >= image.Length)
            {
                logger.LogDebug("Сжатие не уменьшило снимок ({Original} -> {Compressed} байт), сохраняем оригинал",
                    image.Length, compressed.Length);
                return image;
            }

            logger.LogInformation("Снимок сжат перед сохранением: {Original} -> {Compressed} байт",
                image.Length, compressed.Length);
            return compressed;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Не удалось сжать снимок, сохраняем оригинал");
            return image;
        }
    }
}
