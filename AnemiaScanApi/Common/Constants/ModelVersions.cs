namespace AnemiaScanApi.Common.Constants;

/// <summary>
/// Строки версий ML-моделей, записываемые в <see cref="AnemiaScan.ModelVersion"/>.
/// </summary>
public static class ModelVersions
{
    /// <summary>
    /// CIELab-регрессия Hb (GradientBoosting), обученная в
    /// anemia-machine-learning/train_final_model.py на условии C
    /// (394 записи без дублей, leakage-free протокол). Совпадает со
    /// значением "version" в ML/model_card.json рядом с ML/hb_model.onnx —
    /// обновить вместе при переобучении модели.
    /// </summary>
    public const string CielabHemoglobinRegression = "cielab-gb-v1";
}
