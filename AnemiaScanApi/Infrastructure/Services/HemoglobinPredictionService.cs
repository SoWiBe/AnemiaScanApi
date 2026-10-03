using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.ML;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.Infrastructure.Services;

/// <inheritdoc cref="IHemoglobinPredictionService" />
public class HemoglobinPredictionService(
    IEnumerable<IHemoglobinModel> models,
    ILogger<HemoglobinPredictionService> logger)
    : BaseService<HemoglobinPredictionService>(logger), IHemoglobinPredictionService
{
    private readonly IReadOnlyList<IHemoglobinModel> _models = models.ToList();

    public async Task<HemoglobinPrediction?> TryPredictAsync(byte[] imageBytes, int? ageYears,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken);

            var (model, ageInDomain) = SelectModel(ageYears);
            var segmented = ConjunctivaMask.IsSegmented(image);

            var features = CielabFeatureExtractor.Extract(image);
            var hemoglobin = model.Predict(features);

            if (!ageInDomain || !segmented)
            {
                Logger.LogInformation(
                    "Hb посчитан вне области модели {Model}: возраст {Age} в домене={AgeInDomain}, маска={Segmented}",
                    model.Version, ageYears, ageInDomain, segmented);
            }

            return new HemoglobinPrediction(hemoglobin, model.Version, ageInDomain, segmented);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex, "Не удалось посчитать CIELab-регрессию Hb — используется только базовая классификация");
            return null;
        }
    }

    /// <summary>
    /// Модель, в обучающий диапазон которой попадает возраст. Если такой нет —
    /// 5-18 лет, старше 88 или возраст неизвестен — берём взрослую: аудитория
    /// приложения взрослая, и из двух экстраполяций эта ближе к реальности.
    /// Но тогда <c>ageInDomain = false</c>, и число Hb помечается как
    /// недостоверное.
    /// </summary>
    private (IHemoglobinModel Model, bool AgeInDomain) SelectModel(int? ageYears)
    {
        if (ageYears is { } age && _models.FirstOrDefault(m => m.TrainedAgeRange.Contains(age)) is { } covering)
            return (covering, true);

        var fallback = _models.FirstOrDefault(m => m.Version == ModelVersions.CielabHemoglobinRegressionAdults)
                       ?? throw new InvalidOperationException("Взрослая регрессия Hb не зарегистрирована");

        return (fallback, false);
    }
}
