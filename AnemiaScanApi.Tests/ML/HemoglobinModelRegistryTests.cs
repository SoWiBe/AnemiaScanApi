using System.Text.Json;

using AnemiaScanApi.Extensions;
using AnemiaScanApi.ML;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Две регрессии Hb под общим интерфейсом <see cref="IHemoglobinModel"/>:
/// детская (CP-AnemiC) и взрослая (Eyes-defy-anemia). Область определения
/// каждой — часть самой модели.
/// </summary>
public class HemoglobinModelRegistryTests
{
    private static List<IHemoglobinModel> ResolveModels()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHemoglobinPredictionModel();
        services.AddAdultHemoglobinPredictionModel();
        return services.BuildServiceProvider().GetServices<IHemoglobinModel>().ToList();
    }

    [Fact]
    public void BothModelsAreRegisteredUnderInterface()
    {
        var models = ResolveModels();

        models.Should().HaveCount(2);
        models.Should().ContainSingle(m => m is HbOnnxPredictor);
        models.Should().ContainSingle(m => m is HbMlNetPredictor);
    }

    [Fact]
    public void AgeRangesDoNotOverlap()
    {
        // Если бы диапазоны пересекались, выбор модели по возрасту стал бы
        // неоднозначным — для одного пациента подходили бы обе.
        var models = ResolveModels();

        for (var age = 0; age <= 120; age++)
            models.Count(m => m.TrainedAgeRange.Contains(age)).Should().BeLessThanOrEqualTo(1, $"возраст {age}");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void ChildModelCoversPreSchoolAges(int age, bool expected)
        => HbOnnxPredictor.ChildAgeRange.Contains(age).Should().Be(expected);

    [Theory]
    [InlineData(18, false)]
    [InlineData(19, true)]
    [InlineData(88, true)]
    [InlineData(89, false)]
    public void AdultModelCoversExactlyItsTrainingAges(int age, bool expected)
        => HbMlNetPredictor.AdultAgeRange.Contains(age).Should().Be(expected);

    /// <summary>
    /// Версия и возрастной диапазон в коде обязаны совпадать с карточкой модели
    /// рядом с файлом. Карточку пишет тренер при каждом переобучении — если
    /// модель переобучат на другой выборке, а код не обновят, тест это поймает.
    /// </summary>
    [Fact]
    public void AdultModelMatchesItsCard()
    {
        var cardPath = Path.Combine(AppContext.BaseDirectory, "ML", "hb_model_adults.card.json");
        using var card = JsonDocument.Parse(File.ReadAllText(cardPath));

        var version = card.RootElement.GetProperty("version").GetString();
        var ages = card.RootElement.GetProperty("training_data").GetProperty("age_range");

        var adult = ResolveModels().OfType<HbMlNetPredictor>().Single();
        adult.Version.Should().Be(version);
        adult.TrainedAgeRange.Should().Be(new AgeRange(ages[0].GetInt32(), ages[1].GetInt32()));
    }

    [Fact]
    public void ChildModelMatchesItsCard()
    {
        var cardPath = Path.Combine(AppContext.BaseDirectory, "ML", "model_card.json");
        using var card = JsonDocument.Parse(File.ReadAllText(cardPath));

        ResolveModels().OfType<HbOnnxPredictor>().Single().Version
            .Should().Be(card.RootElement.GetProperty("version").GetString());
    }
}
