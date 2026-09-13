using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.ML;
using AnemiaScanApi.Tests.Fixtures;
using AnemiaScanApi.Tests.ML;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnemiaScanApi.Tests.Services;

public class HemoglobinPredictionServiceTests : IDisposable
{
    private readonly HbOnnxPredictor _predictor = new(GoldenFixtures.OnnxModelPath);

    private HemoglobinPredictionService NewService() =>
        new(_predictor, NullLogger<HemoglobinPredictionService>.Instance);

    public static IEnumerable<object[]> GoldenRecords() => GoldenFixtures.AsTheoryData();

    [Theory]
    [MemberData(nameof(GoldenRecords))]
    public async Task TryPredictAsync_MatchesPythonPipeline(ConjunctivaGoldenRecord golden)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(GoldenFixtures.SamplesDir, golden.FileName));
        var service = NewService();

        var result = await service.TryPredictAsync(bytes, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HemoglobinLevel.Should().BeApproximately((float)golden.PredictedHb, 1e-3f);
        result.Severity.Should().Be(SeverityBands.Classify(result.HemoglobinLevel));
    }

    [Fact]
    public async Task TryPredictAsync_ReturnsNullOnInvalidImage()
    {
        var service = NewService();

        var result = await service.TryPredictAsync([1, 2, 3], CancellationToken.None);

        result.Should().BeNull();
    }

    public void Dispose() => _predictor.Dispose();
}
