using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.LLM;
using AnemiaScanApi.Extensions;
using AnemiaScanApi.ML;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// TF-классификатор v13 и решение по порогу из его карточки.
///
/// Модель обучена в AnemiaScanML (train-final) и здесь проходит тот же путь,
/// что в проде: DI, пул движков, TensorFlow-рантайм. Это самый рискованный стык:
/// ошибка в имени колонки или формате модели всплыла бы только на первом скане.
/// </summary>
public class AnemiaClassifierTests
{
    private static readonly string CardPath = Path.Combine(AppContext.BaseDirectory, "LLM",
        Path.ChangeExtension(LLMExtensions.ClassifierModelFile, null) + ".card.json");

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnemiaPredictionModel();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Card_LoadsThresholdAndVersion()
    {
        var card = AnemiaClassifierCard.Load(CardPath);

        card.Version.Should().Be("conjunctiva-v13");
        card.PositiveClass.Should().Be("Low_Hb");
        card.Threshold.Should().Be(0.15f, "порог выбран по кросс-валидации: самый высокий с чувствительностью ≥ 85%");
    }

    [Fact]
    public void Card_MissingFileFailsLoudly()
    {
        // Без карточки неизвестен порог — молча откатываться на argmax нельзя.
        var act = () => AnemiaClassifierCard.Load(Path.Combine(AppContext.BaseDirectory, "нет-такой-карточки.json"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ResolveAnemicIndex_FindsPositiveClassInModelOutput()
    {
        using var provider = BuildProvider();
        var pool = provider.GetRequiredService<PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>>();

        var index = AnemiaClassifier.ResolveAnemicIndex(pool, "Low_Hb");

        index.Should().BeInRange(0, 1, "классов два");
    }

    [Theory]
    [InlineData(0.90f, true)]
    [InlineData(0.20f, true)]   // ниже 0.5, но выше порога 0.15 — argmax назвал бы здоровым
    [InlineData(0.15f, true)]   // граница включительно
    [InlineData(0.10f, false)]
    public void Decide_UsesCardThresholdNotArgmax(float anemiaProbability, bool expectedAnemic)
    {
        using var provider = BuildProvider();
        var classifier = provider.GetRequiredService<AnemiaClassifier>();
        var pool = provider.GetRequiredService<PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>>();
        var anemicIndex = AnemiaClassifier.ResolveAnemicIndex(pool, "Low_Hb");

        var scores = new float[2];
        scores[anemicIndex] = anemiaProbability;
        scores[1 - anemicIndex] = 1 - anemiaProbability;

        var decision = classifier.Decide(new AnemiaPredictionOutput { Score = scores });

        decision.IsAnemic.Should().Be(expectedAnemic);
        decision.AnemiaProbability.Should().Be(anemiaProbability);
        decision.ClassifierVersion.Should().Be("conjunctiva-v13");
    }

    /// <summary>
    /// Сквозной прогон: настоящий снимок конъюнктивы через настоящую модель.
    /// Проверяется не качество — для этого есть кросс-валидация в AnemiaScanML, —
    /// а то, что модель загружается в рантайме API и отдаёт вероятность.
    /// </summary>
    [Fact]
    public void RealModel_ProducesProbabilityForConjunctivaMask()
    {
        using var provider = BuildProvider();
        var pool = provider.GetRequiredService<PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>>();
        var classifier = provider.GetRequiredService<AnemiaClassifier>();

        var sample = Path.Combine(GoldenFixtures.SamplesDir, GoldenFixtures.All[0].FileName);
        var output = pool.Predict(ModelName.SasModel, new AnemiaInput { ImagePath = sample });

        output.Score.Should().NotBeNull().And.HaveCount(2);
        output.Score!.Sum().Should().BeApproximately(1f, 1e-3f, "это распределение по двум классам");

        var decision = classifier.Decide(output);
        decision.AnemiaProbability.Should().BeInRange(0f, 1f);
        decision.IsAnemic.Should().Be(decision.AnemiaProbability >= 0.15f);
    }
}
