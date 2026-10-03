using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.ML;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IPredictionService
{
    /// <summary>
    /// Прогон TF-классификатора и решение по порогу из карточки модели —
    /// см. <see cref="AnemiaClassifier"/>.
    /// </summary>
    Task<ClassifierDecision> PredictAnemiaAsync(PredictionRequest request, CancellationToken cancellationToken);
}
