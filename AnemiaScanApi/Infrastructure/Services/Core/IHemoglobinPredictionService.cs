using AnemiaScanApi.ML;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// CIELab-регрессия Hb — дополнительный путь предсказания рядом с основным
/// TF-классификатором (<see cref="IPredictionService"/>). См.
/// PLAN_pretraining_and_api_integration.md, Этап E: не заменяет основную
/// классификацию, а дополняет её непрерывным значением Hb + severity.
/// </summary>
public interface IHemoglobinPredictionService
{
    /// <summary>
    /// Возвращает null при любом сбое (битое изображение, нет видимых
    /// пикселей и т.п.) вместо исключения — отказ этого пути не должен
    /// ронять основной анализ анемии.
    /// </summary>
    Task<HemoglobinPrediction?> TryPredictAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
