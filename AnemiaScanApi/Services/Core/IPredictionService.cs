using AnemiaScanApi.Models.LLM;

namespace AnemiaScanApi.Services.Core
{
    public interface IPredictionService
    {
        Task<AnemiaPredictionOutput?> PredictAnemiaAsync(CancellationToken cancellationToken);
    }
}