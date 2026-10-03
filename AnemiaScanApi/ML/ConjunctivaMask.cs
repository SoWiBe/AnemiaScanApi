using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.ML;

/// <summary>
/// Отличает вырезанную конъюнктиву от обычного фото.
///
/// Обе CIELab-регрессии обучены на масках: PNG, где вне конъюнктивы фон
/// прозрачный, и <see cref="CielabFeatureExtractor"/> усредняет цвет только по
/// видимой части. У обычного фото (JPEG, или PNG без прозрачности) видимы все
/// пиксели, и усреднение идёт по всему кадру — коже, ресницам, склере. Признаки
/// при этом считаются без ошибок, но из другого распределения, чем при обучении,
/// и число Hb перестаёт быть измерением.
/// </summary>
public static class ConjunctivaMask
{
    /// <summary>
    /// Минимальная доля прозрачных пикселей, чтобы считать снимок маской.
    /// У масок Eyes-defy-anemia прозрачно порядка 80% кадра; 5% отсекает
    /// случайную прозрачность по краю обычного PNG.
    /// </summary>
    public const double MinTransparentFraction = 0.05;

    public static bool IsSegmented(Image<Rgba32> image,
        byte alphaVisibleThreshold = CielabFeatureExtractor.DefaultAlphaVisibleThreshold)
    {
        long transparent = 0;
        var total = (long)image.Width * image.Height;
        if (total == 0) return false;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                foreach (var pixel in accessor.GetRowSpan(y))
                    if (pixel.A < alphaVisibleThreshold) transparent++;
            }
        });

        return (double)transparent / total >= MinTransparentFraction;
    }
}
