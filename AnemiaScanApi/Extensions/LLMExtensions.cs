using AnemiaScanApi.Common.LLM;
using AnemiaScanApi.ML;
using Microsoft.Extensions.ML;

namespace AnemiaScanApi.Extensions;

public static class LLMExtensions
{
    public static IServiceCollection AddAnemiaPredictionModel(this IServiceCollection services)
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "LLM", "anemia_v10_more_aug.zip");

        services.AddPredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>()
            .FromFile(
                modelName: "SASModel",
                filePath: modelPath,
                watchForChanges: true);

        return services;
    }

    /// <summary>
    /// Регистрирует ONNX-модель CIELab-регрессии Hb как singleton.
    /// <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> потокобезопасен
    /// для конкурентных Run() (официальная рекомендация ONNX Runtime), поэтому
    /// singleton, а не пул, как у TF-модели выше. Модель экспортирована из
    /// anemia-machine-learning/export_onnx.py — см. ML/model_card.json рядом
    /// с файлом модели и PLAN_pretraining_and_api_integration.md, Этап D/E.
    /// </summary>
    public static IServiceCollection AddHemoglobinPredictionModel(this IServiceCollection services)
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "ML", "hb_model.onnx");
        services.AddSingleton(_ => new HbOnnxPredictor(modelPath));
        // Под общим интерфейсом — чтобы выбор модели по возрасту видел все
        // регрессии разом через IEnumerable<IHemoglobinModel>.
        services.AddSingleton<IHemoglobinModel>(sp => sp.GetRequiredService<HbOnnxPredictor>());

        return services;
    }

    /// <summary>
    /// Регистрирует CIELab-регрессию Hb, обученную на взрослых
    /// (<c>ML/hb_model_adults.zip</c>, карточка — <c>hb_model_adults.card.json</c>).
    ///
    /// В отличие от ONNX-модели выше — через пул: PredictionEngine в ML.NET
    /// не потокобезопасен. Сам предиктор при этом singleton, пул внутри него
    /// раздаёт движки по запросам.
    /// </summary>
    public static IServiceCollection AddAdultHemoglobinPredictionModel(this IServiceCollection services)
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "ML", "hb_model_adults.zip");

        services.AddPredictionEnginePool<HbAdultModelInput, HbAdultModelOutput>()
            .FromFile(
                modelName: HbMlNetPredictor.ModelName,
                filePath: modelPath,
                watchForChanges: false);

        services.AddSingleton<HbMlNetPredictor>();
        services.AddSingleton<IHemoglobinModel>(sp => sp.GetRequiredService<HbMlNetPredictor>());

        return services;
    }
}