using AnemiaScanApi.Common.Constants;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AnemiaScanApi.ML;

/// <summary>
/// Инференс регрессии Hb по CIELab-признакам через ONNX Runtime — детская
/// модель (CP-AnemiC, 6-59 месяцев).
/// Модель экспортирована из anemia-machine-learning/export_onnx.py
/// (GradientBoostingRegressor -> ONNX, opset 17) — числовой паритет с
/// sklearn проверен там же (расхождение ~1e-6, см. model/model_card.json).
/// </summary>
public sealed class HbOnnxPredictor : IHemoglobinModel, IDisposable
{
    /// <summary>
    /// CP-AnemiC — 6-59 месяцев. Возраст в профиле считается в полных годах,
    /// поэтому граница 0-4 года: младенцы до 6 месяцев в неё формально
    /// попадают, хотя в обучающей выборке их нет.
    /// </summary>
    public static readonly AgeRange ChildAgeRange = new(0, 4);

    public string Version => ModelVersions.CielabHemoglobinRegression;

    public AgeRange TrainedAgeRange => ChildAgeRange;

    private readonly InferenceSession _session;
    private readonly string _inputName;

    public HbOnnxPredictor(string onnxModelPath)
    {
        _session = new InferenceSession(onnxModelPath);
        _inputName = _session.InputMetadata.Keys.First();
    }

    public float Predict(CielabFeatures features)
    {
        var input = features.ToModelInput();
        var tensor = new DenseTensor<float>(input, [1, input.Length]);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };

        using var results = _session.Run(inputs);
        return results.First().AsEnumerable<float>().First();
    }

    public void Dispose() => _session.Dispose();
}
