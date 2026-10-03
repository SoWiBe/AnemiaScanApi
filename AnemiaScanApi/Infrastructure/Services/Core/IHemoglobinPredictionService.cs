using AnemiaScanApi.ML;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// CIELab-регрессия Hb — дополнительный путь предсказания рядом с основным
/// TF-классификатором (<see cref="IPredictionService"/>): не заменяет его
/// вердикт, а дополняет непрерывным значением Hb.
///
/// Регрессий две — детская и взрослая; какая считает, решает возраст пациента.
/// </summary>
public interface IHemoglobinPredictionService
{
    /// <summary>
    /// Возвращает null при любом сбое (битое изображение, нет видимых
    /// пикселей и т.п.) вместо исключения — отказ этого пути не должен
    /// ронять основной анализ анемии.
    /// </summary>
    /// <param name="ageYears">Возраст пациента; null — профиль неполон.</param>
    Task<HemoglobinPrediction?> TryPredictAsync(byte[] imageBytes, int? ageYears,
        CancellationToken cancellationToken);
}
