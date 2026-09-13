namespace AnemiaScanApi.ML;

/// <summary>
/// Признаки CIELab, посчитанные по видимой (не замаскированной альфой)
/// части изображения конъюнктивы. Порядок полей соответствует порядку
/// признаков в обученной модели (см. model/model_card.json в
/// anemia-machine-learning — features.columns).
/// </summary>
public readonly record struct CielabFeatures(double L, double A, double B)
{
    /// <summary>Признаки в порядке, ожидаемом ONNX-моделью: [L, A, B].</summary>
    public float[] ToModelInput() => [(float)L, (float)A, (float)B];
}
