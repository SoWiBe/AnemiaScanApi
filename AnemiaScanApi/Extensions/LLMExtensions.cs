using AnemiaScanApi.Common.LLM;
using AnemiaScanApi.ML;
using Microsoft.Extensions.ML;

namespace AnemiaScanApi.Extensions;

public static class LLMExtensions
{
    /// <summary>Файл классификатора. Карточка с порогом — рядом, с суффиксом .card.json.</summary>
    public const string ClassifierModelFile = "anemia_v13_conjunctiva_full.zip";

    /// <summary>
    /// TF-классификатор (Inception + LightGBM) и его карточка с порогом решения.
    ///
    /// Модель v13 обучена в AnemiaScanML (команда train-final) на всех 215
    /// пациентах по вырезанной конъюнктиве; ожидаемое качество и порог — в
    /// карточке. Вход — снимок по контракту docs/image-upload-contract.md.
    ///
    /// watchForChanges выключен: модель и карточка с порогом — пара. Горячая
    /// перезагрузка подменила бы модель, оставив старый порог, прочитанный при
    /// старте. Обе меняются только вместе, через деплой с перезапуском.
    ///
    /// Карточка читается здесь же, при регистрации: если её нет, приложение не
    /// стартует, а не работает молча по argmax.
    /// </summary>
    public static IServiceCollection AddAnemiaPredictionModel(this IServiceCollection services)
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "LLM", ClassifierModelFile);
        var card = AnemiaClassifierCard.Load(Path.ChangeExtension(modelPath, null) + ".card.json");

        services.AddPredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>()
            .FromFile(
                modelName: Common.Constants.ModelName.SasModel,
                filePath: modelPath,
                watchForChanges: false);

        services.AddSingleton(card);
        services.AddSingleton<AnemiaClassifier>();

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