using AnemiaScanApi.Models.LLM;
using AnemiaScanApi.Models.Requests;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IPredictionService
{
    Task<AnemiaPredictionOutput> PredictAnemiaAsync(PredictionRequest request, CancellationToken cancellationToken);
}
