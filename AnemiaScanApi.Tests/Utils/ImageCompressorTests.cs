using AnemiaScanApi.Settings;
using AnemiaScanApi.Utils;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.Tests.Utils;

/// <summary>
/// Downscale снимка перед GridFS (P0 №9 в docs/plans/MVP_PLAN.md): оригиналы с
/// телефона по 2-3 МБ в облачной Mongo — это прямые деньги и упор в лимит тарифа.
/// </summary>
public class ImageCompressorTests
{
    private const int MaxDimension = 1024;

    private static ImageCompressor NewCompressor(int maxDimension = MaxDimension, int jpegQuality = 85)
        => new(Options.Create(new ImageStorageSettings { MaxDimension = maxDimension, JpegQuality = jpegQuality }),
            NullLogger<ImageCompressor>.Instance);

    /// <summary>
    /// Шумное изображение, а не однотонная заливка: сплошной цвет сжимается в
    /// килобайты и не показал бы разницы между оригиналом и результатом.
    /// </summary>
    private static byte[] NoisyPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var random = new Random(42);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        });

        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    [Fact]
    public void CompressForStorage_ScalesDownOversizedImage()
    {
        var original = NoisyPng(2048, 1536);

        var compressed = NewCompressor().CompressForStorage(original);

        using var result = Image.Load(compressed);
        result.Width.Should().Be(MaxDimension);
        result.Height.Should().Be(768, "пропорции 4:3 должны сохраниться");
        compressed.Length.Should().BeLessThan(original.Length);
    }

    [Fact]
    public void CompressForStorage_KeepsSmallImageDimensions()
    {
        var original = NoisyPng(640, 480);

        var compressed = NewCompressor().CompressForStorage(original);

        using var result = Image.Load(compressed);
        result.Width.Should().Be(640);
        result.Height.Should().Be(480);
    }

    [Fact]
    public void CompressForStorage_ReturnsOriginalWhenCompressionDoesNotHelp()
    {
        // Маленький PNG-шум: JPEG на таком размере обычно не выигрывает,
        // и отдавать наружу больший по весу файл смысла нет.
        var original = NoisyPng(16, 16);

        var compressed = NewCompressor().CompressForStorage(original);

        compressed.Length.Should().BeLessThanOrEqualTo(original.Length);
    }

    [Fact]
    public void CompressForStorage_ReturnsOriginalOnBrokenImage()
    {
        // Потеря снимка хуже лишних мегабайт в базе — битые байты уходят как есть.
        byte[] notAnImage = [1, 2, 3, 4, 5];

        NewCompressor().CompressForStorage(notAnImage).Should().Equal(notAnImage);
    }

    [Fact]
    public void CompressForStorage_ReturnsEmptyInputAsIs()
        => NewCompressor().CompressForStorage([]).Should().BeEmpty();
}
