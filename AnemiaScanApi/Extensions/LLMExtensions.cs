using AnemiaScanApi.Models.LLM;
using Microsoft.Extensions.ML;

namespace AnemiaScanApi.Extensions;

public static class LLMExtensions
{
    public static IServiceCollection AddAnemiaPredictionModel(this IServiceCollection services)
    {
        services.AddPredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>()
            .FromFile(
                modelName: "SASModel", 
                filePath: @"C:\Users\aleks\Documents\Anemia Scan Project\AnemiaScanApi\AnemiaScanApi\LLM\anemia_v10_more_aug.zip", 
                watchForChanges: true);

        return services;
    }
}