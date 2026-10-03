using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Extensions;
using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.ML;
using AnemiaScanApi.Tests.Fixtures;
using AnemiaScanApi.Tests.ML;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.Tests.Services;

/// <summary>
/// CIELab-регрессия Hb: выбор одной из двух моделей по возрасту и проверка,
/// что на входе вырезанная конъюнктива, а не целый кадр.
/// </summary>
public class HemoglobinPredictionServiceTests
{
    private const int ChildAge = 2;
    private const int AdultAge = 40;

    private static HemoglobinPredictionService NewService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHemoglobinPredictionModel();
        services.AddAdultHemoglobinPredictionModel();

        var models = services.BuildServiceProvider().GetServices<IHemoglobinModel>();
        return new HemoglobinPredictionService(models, NullLogger<HemoglobinPredictionService>.Instance);
    }

    public static IEnumerable<object[]> GoldenRecords() => GoldenFixtures.AsTheoryData();

    private static Task<byte[]> SampleMaskAsync()
        => File.ReadAllBytesAsync(Path.Combine(GoldenFixtures.SamplesDir, GoldenFixtures.All[0].FileName));

    /// <summary>
    /// Golden-фикстуры посчитаны детской моделью в Python. С детским возрастом
    /// сервис обязан выбрать именно её и воспроизвести эти значения.
    /// </summary>
    [Theory]
    [MemberData(nameof(GoldenRecords))]
    public async Task TryPredictAsync_ChildMatchesPythonPipeline(ConjunctivaGoldenRecord golden)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(GoldenFixtures.SamplesDir, golden.FileName));

        var result = await NewService().TryPredictAsync(bytes, ChildAge, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ModelVersion.Should().Be(ModelVersions.CielabHemoglobinRegression);
        result.HemoglobinLevel.Should().BeApproximately((float)golden.PredictedHb, 1e-3f);
    }

    [Fact]
    public async Task TryPredictAsync_UsesAdultModelForAdult()
    {
        var result = await NewService().TryPredictAsync(await SampleMaskAsync(), AdultAge, CancellationToken.None);

        result!.ModelVersion.Should().Be(ModelVersions.CielabHemoglobinRegressionAdults);
        result.AgeInDomain.Should().BeTrue();
    }

    [Theory]
    [InlineData(10)]   // 5-18: ни одна модель такой возраст не видела
    [InlineData(17)]
    [InlineData(95)]   // старше обучающей выборки взрослых (88)
    [InlineData(null)] // профиль неполон
    public async Task TryPredictAsync_FallsBackToAdultOutsideAnyDomain(int? age)
    {
        var result = await NewService().TryPredictAsync(await SampleMaskAsync(), age, CancellationToken.None);

        // Аудитория взрослая — из двух экстраполяций берём взрослую, но честно
        // помечаем, что пациент вне её обучающего диапазона.
        result!.ModelVersion.Should().Be(ModelVersions.CielabHemoglobinRegressionAdults);
        result.AgeInDomain.Should().BeFalse();
        result.InDomain.Should().BeFalse();
    }

    [Fact]
    public async Task TryPredictAsync_RecognizesSegmentedConjunctiva()
    {
        var result = await NewService().TryPredictAsync(await SampleMaskAsync(), AdultAge, CancellationToken.None);

        result!.InputSegmented.Should().BeTrue();
        result.InDomain.Should().BeTrue("возраст в диапазоне и на входе маска");
    }

    /// <summary>
    /// Тот же снимок, но пересохранённый в JPEG: альфа-канал пропадает, и
    /// экстрактор усредняет цвет по всему кадру. Число посчитается, но
    /// измерением оно не будет — флаг обязан это отразить.
    /// </summary>
    [Fact]
    public async Task TryPredictAsync_FlagsWholeFrameAsOutOfDomain()
    {
        using var mask = Image.Load<Rgba32>(await SampleMaskAsync());
        using var jpeg = new MemoryStream();
        await mask.SaveAsync(jpeg, new JpegEncoder());

        var result = await NewService().TryPredictAsync(jpeg.ToArray(), AdultAge, CancellationToken.None);

        result.Should().NotBeNull("целый кадр не повод отказывать в оценке");
        result!.InputSegmented.Should().BeFalse();
        result.AgeInDomain.Should().BeTrue();
        result.InDomain.Should().BeFalse();
    }

    [Fact]
    public async Task TryPredictAsync_ReturnsNullOnInvalidImage()
    {
        var result = await NewService().TryPredictAsync([1, 2, 3], AdultAge, CancellationToken.None);

        result.Should().BeNull();
    }
}
