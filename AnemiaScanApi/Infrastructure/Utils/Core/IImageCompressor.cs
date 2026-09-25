namespace AnemiaScanApi.Infrastructure.Utils.Core;

/// <summary>
/// Уменьшает снимок перед укладкой в GridFS.
/// </summary>
public interface IImageCompressor
{
    /// <summary>
    /// Ужимает изображение до настроенной максимальной стороны и перекодирует в JPEG.
    /// Возвращает исходные байты, если сжать не удалось, — потеря снимка хуже,
    /// чем лишние мегабайты в базе.
    /// </summary>
    byte[] CompressForStorage(byte[] image);
}
