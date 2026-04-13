using AnemiaScanApi.Common.LLM;
using Microsoft.Extensions.ML;

namespace AnemiaScanApi.Extensions;

public static class LLMExtensions
{
    public static IServiceCollection AddAnemiaPredictionModel(this IServiceCollection services)
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "../LLM", "anemia_v10_more_aug.zip");
        
        services.AddPredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>()
            .FromFile(
                modelName: "SASModel", 
                filePath: modelPath, 
                watchForChanges: true);

        return services;
    }
}