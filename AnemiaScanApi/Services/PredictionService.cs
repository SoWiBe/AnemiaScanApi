using AnemiaScanApi.Models.LLM;
using AnemiaScanApi.Services.Core;

namespace AnemiaScanApi.Services;

public class PredictionService(ILogger<PredictionService> logger) : BaseService<PredictionService>(logger), IPredictionService
{
    public async Task<AnemiaPredictionOutput?> PredictAnemiaAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}