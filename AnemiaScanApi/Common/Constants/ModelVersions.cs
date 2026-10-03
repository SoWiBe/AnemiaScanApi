namespace AnemiaScanApi.Common.Constants;

/// <summary>
/// Строки версий ML-моделей, записываемые в <see cref="AnemiaScan.ModelVersion"/>.
/// По ним в истории видно, какая именно модель посчитала Hb для скана.
/// </summary>
public static class ModelVersions
{
    /// <summary>
    /// CIELab-регрессия Hb (GradientBoosting), обученная в
    /// anemia-machine-learning/train_final_model.py на условии C
    /// (394 записи без дублей, leakage-free протокол) — дети 6-59 месяцев,
    /// CP-AnemiC. Совпадает со значением "version" в ML/model_card.json
    /// рядом с ML/hb_model.onnx — обновить вместе при переобучении модели.
    /// </summary>
    public const string CielabHemoglobinRegression = "cielab-gb-v1";

    /// <summary>
    /// CIELab-регрессия Hb (FastTree), обученная в AnemiaScanML командой
    /// train-hb на Eyes-defy-anemia — 215 взрослых 19-88 лет. Совпадает со
    /// значением "version" в ML/hb_model_adults.card.json — обновить вместе
    /// при переобучении модели.
    /// </summary>
    public const string CielabHemoglobinRegressionAdults = "cielab-gb-adults-v1";
}
