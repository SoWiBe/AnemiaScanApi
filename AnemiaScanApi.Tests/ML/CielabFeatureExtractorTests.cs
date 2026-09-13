using AnemiaScanApi.ML;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Golden-тест: CielabFeatureExtractor (C#) должен воспроизводить
/// anemia-machine-learning/features.py::extract_cielab_from_image (Python)
/// на тех же изображениях с точностью, достаточной для того, чтобы модель
/// (обученная на Python-признаках) не деградировала при переносе на C#.
/// См. PLAN_pretraining_and_api_integration.md, Этап D.
/// </summary>
public class CielabFeatureExtractorTests
{
    // Эмпирически (см. историю Этапа D) ImageSharp декодирует PNG идентично
    // PIL, и порт формул sRGB->CIELab совпадает с skimage.color.rgb2lab
    // точно — на всех 22 golden-изображениях расхождение 0.00000 на 5
    // знаках. Допуск оставлен небольшим (не нулевым) на случай иного
    // порядка суммирования float/double на других платформах/CPU, а не
    // потому что расхождение реально ожидается такого масштаба.
    private const double Tolerance = 1e-4;

    public static IEnumerable<object[]> GoldenRecords() => GoldenFixtures.AsTheoryData();

    [Theory]
    [MemberData(nameof(GoldenRecords))]
    public void Extract_MatchesPythonReference(ConjunctivaGoldenRecord golden)
    {
        var path = Path.Combine(GoldenFixtures.SamplesDir, golden.FileName);

        var features = CielabFeatureExtractor.Extract(path);

        features.L.Should().BeApproximately(golden.L, Tolerance, "L канал должен совпадать с Python-эталоном");
        features.A.Should().BeApproximately(golden.A, Tolerance, "a канал должен совпадать с Python-эталоном");
        features.B.Should().BeApproximately(golden.B, Tolerance, "b канал должен совпадать с Python-эталоном");
    }

    [Fact]
    public void Extract_ThrowsWhenNoVisiblePixels()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4);
        // весь холст по умолчанию alpha=0 — нет видимых пикселей

        var act = () => CielabFeatureExtractor.Extract(image);

        act.Should().Throw<InvalidOperationException>();
    }
}
