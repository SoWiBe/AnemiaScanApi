using System.Globalization;

using AnemiaScanApi.Extensions;
using AnemiaScanApi.ML;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Взрослая регрессия Hb (<c>ML/hb_model_adults.zip</c>) через тот же путь
/// регистрации, что и в проде: DI, пул движков, загрузка FastTree-модели.
///
/// Фикстура — признаки и лабораторный Hb тех самых 215 взрослых, на которых
/// модель обучена. Поэтому здесь проверяется не обобщение (его меряет
/// кросс-валидация в AnemiaScanML: MAE и r записаны в карточке модели), а то, что модель в API
/// вообще работает так же, как при обучении. Главный риск, который ловит тест, —
/// неверное сопоставление колонок: ML.NET связывает признаки по имени свойства,
/// и перепутанные L/A/B не дают ошибки, а дают мусорные предсказания.
/// </summary>
public class HbMlNetPredictorTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "cielab_features_adults.csv");

    private sealed record Row(string Patient, CielabFeatures Features, double LabHb);

    private static List<Row> LoadRows() => File.ReadLines(FixturePath)
        .Skip(1)
        .Select(line => line.Split(','))
        .Select(cols => new Row(
            cols[0],
            new CielabFeatures(Parse(cols[4]), Parse(cols[5]), Parse(cols[6])),
            Parse(cols[7])))
        .ToList();

    private static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private static HbMlNetPredictor ResolvePredictor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdultHemoglobinPredictionModel();
        return services.BuildServiceProvider().GetRequiredService<HbMlNetPredictor>();
    }

    [Fact]
    public void Predict_LoadsModelThroughProductionRegistration()
    {
        var predictor = ResolvePredictor();
        var row = LoadRows().First();

        var hb = predictor.Predict(row.Features);

        hb.Should().BeInRange(3f, 20f, "предсказание должно быть правдоподобным значением Hb");
    }

    [Fact]
    public void Predict_TracksLabHemoglobinOnTrainingPatients()
    {
        var predictor = ResolvePredictor();
        var rows = LoadRows();

        rows.Should().HaveCount(215);

        var predicted = rows.Select(r => (double)predictor.Predict(r.Features)).ToArray();
        var actual = rows.Select(r => r.LabHb).ToArray();

        var mae = predicted.Zip(actual, (p, a) => Math.Abs(p - a)).Average();
        var r = Pearson(predicted, actual);

        // На обучающих пациентах связь заведомо сильнее, чем out-of-fold.
        // Порог проверен подменой: у правильной модели r = 0.975, а если в
        // предикторе перепутать L и B — 0.692. Не к нулю: признак a остаётся на
        // месте и сам по себе связан с Hb. При переобучении модели эту проверку
        // стоит повторить.
        r.Should().BeGreaterThan(0.8, $"in-sample корреляция = {r:F3}");
        // Out-of-fold MAE берём из карточки модели, а не хардкодим: карточку
        // переписывает тренер при каждом переобучении.
        var outOfFoldMae = ReadCardMae();
        mae.Should().BeLessThan(outOfFoldMae, $"in-sample MAE = {mae:F3} не может быть хуже out-of-fold {outOfFoldMae:F3}");
    }

    [Fact]
    public void Predict_IsSafeUnderConcurrentCalls()
    {
        // PredictionEngine в ML.NET не потокобезопасен — поэтому пул. Тест
        // проверяет, что параллельные запросы получают те же ответы, что и
        // последовательные.
        var predictor = ResolvePredictor();
        var rows = LoadRows().Take(40).ToList();

        var sequential = rows.Select(r => predictor.Predict(r.Features)).ToArray();
        var parallel = new float[rows.Count];

        Parallel.For(0, rows.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            i => parallel[i] = predictor.Predict(rows[i].Features));

        parallel.Should().Equal(sequential);
    }

    private static double ReadCardMae()
    {
        var cardPath = Path.Combine(AppContext.BaseDirectory, "ML", "hb_model_adults.card.json");
        using var card = System.Text.Json.JsonDocument.Parse(File.ReadAllText(cardPath));
        return card.RootElement.GetProperty("evaluation").GetProperty("mae").GetDouble();
    }

    private static double Pearson(double[] x, double[] y)
    {
        var meanX = x.Average();
        var meanY = y.Average();
        var covariance = x.Zip(y, (a, b) => (a - meanX) * (b - meanY)).Sum();
        var denominator = Math.Sqrt(x.Sum(a => (a - meanX) * (a - meanX)) * y.Sum(b => (b - meanY) * (b - meanY)));
        return denominator > 0 ? covariance / denominator : 0;
    }
}
