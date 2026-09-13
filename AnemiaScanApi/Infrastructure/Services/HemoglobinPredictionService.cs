using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.ML;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.Infrastructure.Services;

/// <inheritdoc cref="IHemoglobinPredictionService" />
public class HemoglobinPredictionService(HbOnnxPredictor predictor, ILogger<HemoglobinPredictionService> logger)
    : BaseService<HemoglobinPredictionService>(logger), IHemoglobinPredictionService
{
    public async Task<HemoglobinPrediction?> TryPredictAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken);

            var features = CielabFeatureExtractor.Extract(image);
            var hemoglobin = predictor.Predict(features);
            var severity = SeverityBands.Classify(hemoglobin);

            return new HemoglobinPrediction(hemoglobin, severity);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex, "Не удалось посчитать CIELab-регрессию Hb — используется только базовая классификация");
            return null;
        }
    }
}
