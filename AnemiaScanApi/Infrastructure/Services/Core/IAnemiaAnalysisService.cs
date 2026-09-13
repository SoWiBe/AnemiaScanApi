using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.ML;
using MongoDB.Bson;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Interface for ML analysis service operations.
/// </summary>
public interface IAnemiaAnalysisService
{
    /// <param name="hemoglobinPrediction">
    /// Результат <see cref="IHemoglobinPredictionService"/> — null, если тот
    /// путь не смог посчитать признаки для этого изображения (не блокирует
    /// запись основного результата TF-классификатора).
    /// </param>
    Task<AnalyseAnemiaResponse> WriteAnalyseAsync(
        Guid userId, float score, string predictionLabel, byte[] image,
        HemoglobinPrediction? hemoglobinPrediction, CancellationToken cancellationToken);
    Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default);
}