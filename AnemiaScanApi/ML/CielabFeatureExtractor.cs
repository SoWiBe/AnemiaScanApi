using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.ML;

/// <summary>
/// Порт anemia-machine-learning/features.py::extract_cielab_from_image на C#.
/// Должен воспроизводить Python-реализацию 1:1 — модель обучена на признаках,
/// посчитанных этой формулой (skimage.color.rgb2lab), а не абстрактным CIELab
/// вообще. Golden-тест (AnemiaScanApi.Tests/CielabFeatureExtractorTests.cs)
/// сверяет вывод с эталонными значениями из Python на тех же изображениях.
/// </summary>
public static class CielabFeatureExtractor
{
    /// <summary>
    /// Порог непрозрачности: пиксели с alpha ниже этого значения — это фон/
    /// невидимая часть маски ROI, а не ткань конъюнктивы, и в среднее не
    /// включаются. Должен совпадать с features.ALPHA_VISIBLE_THRESHOLD в
    /// Python — см. PLAN_pretraining_and_api_integration.md, §0.1 (подобран
    /// и проверен побитово против data/datacard.json).
    /// </summary>
    public const byte DefaultAlphaVisibleThreshold = 250;

    // sRGB (D65) -> XYZ, стандартная матрица из skimage.color.colorconv.xyz_from_rgb.
    private static readonly double[,] XyzFromRgb =
    {
        { 0.412453, 0.357580, 0.180423 },
        { 0.212671, 0.715160, 0.072169 },
        { 0.019334, 0.119193, 0.950227 },
    };

    // Опорная белая точка D65, наблюдатель 2° — skimage.color.colorconv.illuminants["D65"]["2"].
    private const double Xn = 0.95047;
    private const double Yn = 1.0;
    private const double Zn = 1.08883;

    public static CielabFeatures Extract(string imagePath, byte alphaVisibleThreshold = DefaultAlphaVisibleThreshold)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        return Extract(image, alphaVisibleThreshold);
    }

    public static CielabFeatures Extract(Image<Rgba32> image, byte alphaVisibleThreshold = DefaultAlphaVisibleThreshold)
    {
        double sumL = 0, sumA = 0, sumB = 0;
        long visibleCount = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.A < alphaVisibleThreshold) continue;

                    var (l, a, b) = ToLab(pixel.R, pixel.G, pixel.B);
                    sumL += l;
                    sumA += a;
                    sumB += b;
                    visibleCount++;
                }
            }
        });

        if (visibleCount == 0)
        {
            throw new InvalidOperationException(
                $"Нет видимых пикселей (alpha >= {alphaVisibleThreshold}) в изображении.");
        }

        return new CielabFeatures(sumL / visibleCount, sumA / visibleCount, sumB / visibleCount);
    }

    /// <summary>
    /// sRGB [0-255] -> CIELab одного пикселя (linearize -> XYZ -> Lab).
    /// Повторяет skimage.color.rgb2lab, которым считались признаки при
    /// обучении модели — не «CIELab вообще», а конкретно эту реализацию.
    /// </summary>
    private static (double L, double A, double B) ToLab(byte r, byte g, byte b)
    {
        var rl = Linearize(r / 255.0);
        var gl = Linearize(g / 255.0);
        var bl = Linearize(b / 255.0);

        var x = XyzFromRgb[0, 0] * rl + XyzFromRgb[0, 1] * gl + XyzFromRgb[0, 2] * bl;
        var y = XyzFromRgb[1, 0] * rl + XyzFromRgb[1, 1] * gl + XyzFromRgb[1, 2] * bl;
        var z = XyzFromRgb[2, 0] * rl + XyzFromRgb[2, 1] * gl + XyzFromRgb[2, 2] * bl;

        var fx = LabF(x / Xn);
        var fy = LabF(y / Yn);
        var fz = LabF(z / Zn);

        var l = 116.0 * fy - 16.0;
        var a = 500.0 * (fx - fy);
        var bb = 200.0 * (fy - fz);
        return (l, a, bb);
    }

    private static double Linearize(double c) =>
        c > 0.04045 ? Math.Pow((c + 0.055) / 1.055, 2.4) : c / 12.92;

    private static double LabF(double t) =>
        t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116.0;
}
