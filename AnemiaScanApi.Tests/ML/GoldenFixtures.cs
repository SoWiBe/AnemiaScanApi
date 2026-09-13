using System.Text.Json;
using AnemiaScanApi.Tests.Fixtures;

namespace AnemiaScanApi.Tests.ML;

internal static class GoldenFixtures
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    public static readonly string SamplesDir = Path.Combine(FixturesDir, "ConjunctivaSamples");
    public static readonly string OnnxModelPath = Path.Combine(FixturesDir, "hb_model.onnx");

    private static readonly Lazy<IReadOnlyList<ConjunctivaGoldenRecord>> Records = new(Load);

    public static IEnumerable<object[]> AsTheoryData() => Records.Value.Select(r => new object[] { r });

    private static List<ConjunctivaGoldenRecord> Load()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "conjunctiva_golden.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<ConjunctivaGoldenRecord>>(json, options)
               ?? throw new InvalidOperationException("conjunctiva_golden.json пуст или не распарсился");
    }
}
