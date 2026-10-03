using Microsoft.Extensions.ML;
using Microsoft.ML.Data;

namespace AnemiaScanApi.ML;

/// <summary>
/// Вход модели взрослой регрессии Hb. Имена свойств обязаны совпадать с
/// именами колонок, на которых модель обучалась (AnemiaScanML,
/// <c>HbSample.L/A/B</c>): ML.NET сопоставляет их по имени, и опечатка здесь
/// всплывёт только исключением при первом предсказании.
/// </summary>
public sealed class HbAdultModelInput
{
    public float L { get; set; }
    public float A { get; set; }
    public float B { get; set; }
}

public sealed class HbAdultModelOutput
{
    [ColumnName("Score")] public float Hemoglobin { get; set; }
}

/// <summary>
/// Инференс CIELab-регрессии Hb, обученной на взрослых
/// (<c>ML/hb_model_adults.zip</c>, карточка рядом).
///
/// Отличия от <see cref="HbOnnxPredictor"/>:
///
/// - формат ML.NET, а не ONNX — экспорт в ONNX под net10.0 недоступен;
/// - <c>PredictionEngine</c> в ML.NET не потокобезопасен (в отличие от
///   <c>InferenceSession</c> ONNX Runtime), поэтому предсказания идут через
///   пул — так же, как у TF-классификатора.
///
/// Признаки на входе обязаны считаться тем же <see cref="CielabFeatureExtractor"/>,
/// что и при обучении: в AnemiaScanML он подключён ссылкой на этот самый
/// исходник, а не скопирован.
/// </summary>
public sealed class HbMlNetPredictor(PredictionEnginePool<HbAdultModelInput, HbAdultModelOutput> pool)
    : IHemoglobinModel
{
    /// <summary>Имя модели в пуле.</summary>
    public const string ModelName = "HbAdults";

    /// <summary>
    /// Ровно диапазон обучающей выборки Eyes-defy-anemia (см. age_range в
    /// hb_model_adults.card.json). Подростки 15-18 по шкале ВОЗ уже взрослые,
    /// но модель их не видела — для них число Hb остаётся экстраполяцией.
    /// </summary>
    public static readonly AgeRange AdultAgeRange = new(19, 88);

    public string Version => Common.Constants.ModelVersions.CielabHemoglobinRegressionAdults;

    public AgeRange TrainedAgeRange => AdultAgeRange;

    public float Predict(CielabFeatures features)
    {
        var input = new HbAdultModelInput
        {
            L = (float)features.L,
            A = (float)features.A,
            B = (float)features.B
        };

        return pool.Predict(ModelName, input).Hemoglobin;
    }
}
