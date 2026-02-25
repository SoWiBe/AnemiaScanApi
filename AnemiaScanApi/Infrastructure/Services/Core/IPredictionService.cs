using AnemiaScanApi.Common.LLM;
using AnemiaScanApi.Common.Requests;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IPredictionService
{
    Task<AnemiaPredictionOutput> PredictAnemiaAsync(PredictionRequest request, CancellationToken cancellationToken);
}
