using AnemiaScanApi.ML;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Сквозной golden-тест: изображение -> CielabFeatureExtractor (C#) ->
/// HbOnnxPredictor (ONNX Runtime) должен давать тот же Hb, что и
/// изображение -> features.py -> model/hb_model.joblib (Python) —
/// в допуске, покрывающем и расхождение признаков (см.
/// CielabFeatureExtractorTests), и ONNX/sklearn паритет (~1e-6, см.
/// export_onnx.py).
/// </summary>
public class HbOnnxPredictorTests
{
    // Эмпирически расхождение с Python-пайплайном (features.py + sklearn ->
    // export_onnx.py -> ONNX Runtime) на всех 22 golden-записях — 0.00000
    // (см. историю Этапа D). Допуск покрывает только float32-округление
    // ONNX Runtime (паритет с sklearn ~1e-6, см. export_onnx.py), не более.
    private const float HbTolerance = 1e-3f;

    public static IEnumerable<object[]> GoldenRecords() => GoldenFixtures.AsTheoryData();

    [Theory]
    [MemberData(nameof(GoldenRecords))]
    public void Predict_MatchesPythonPipeline(ConjunctivaGoldenRecord golden)
    {
        var path = Path.Combine(GoldenFixtures.SamplesDir, golden.FileName);
        var features = CielabFeatureExtractor.Extract(path);

        using var predictor = new HbOnnxPredictor(GoldenFixtures.OnnxModelPath);
        var predictedHb = predictor.Predict(features);

        predictedHb.Should().BeApproximately((float)golden.PredictedHb, HbTolerance,
            $"полный пайплайн для {golden.ImageId} должен воспроизводить Python-предсказание");
    }
}
