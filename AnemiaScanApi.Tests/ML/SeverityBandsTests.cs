using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.ML;

using FluentAssertions;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Шкала тяжести по полу и возрасту (P0 №10 в docs/plans/MVP_PLAN.md).
/// Источник границ — WHO, Haemoglobin concentrations for the diagnosis of
/// anaemia and assessment of severity (2011).
/// </summary>
public class SeverityBandsTests
{
    private const string Severe = "Severe";
    private const string Moderate = "Moderate";
    private const string Mild = "Mild";
    private const string NonAnemic = "Non-Anemic";

    [Theory]
    // Дети 6-59 месяцев: норма от 11.0
    [InlineData(6.9f, 2, Sex.Female, Severe)]
    [InlineData(7.0f, 2, Sex.Female, Moderate)]
    [InlineData(9.9f, 2, Sex.Male, Moderate)]
    [InlineData(10.0f, 2, Sex.Male, Mild)]
    [InlineData(10.9f, 4, Sex.Female, Mild)]
    [InlineData(11.0f, 4, Sex.Female, NonAnemic)]
    // Дети 5-11 лет: норма от 11.5
    [InlineData(10.9f, 8, Sex.Male, Moderate)]
    [InlineData(11.0f, 8, Sex.Male, Mild)]
    [InlineData(11.4f, 8, Sex.Female, Mild)]
    [InlineData(11.5f, 8, Sex.Female, NonAnemic)]
    // Подростки 12-14 лет: норма от 12.0
    [InlineData(11.9f, 13, Sex.Male, Mild)]
    [InlineData(12.0f, 13, Sex.Male, NonAnemic)]
    // Женщины от 15: норма от 12.0
    [InlineData(7.9f, 30, Sex.Female, Severe)]
    [InlineData(8.0f, 30, Sex.Female, Moderate)]
    [InlineData(10.9f, 30, Sex.Female, Moderate)]
    [InlineData(11.0f, 30, Sex.Female, Mild)]
    [InlineData(11.9f, 30, Sex.Female, Mild)]
    [InlineData(12.0f, 30, Sex.Female, NonAnemic)]
    // Мужчины от 15: норма от 13.0
    [InlineData(11.9f, 30, Sex.Male, Mild)]
    [InlineData(12.9f, 30, Sex.Male, Mild)]
    [InlineData(13.0f, 30, Sex.Male, NonAnemic)]
    public void Classify_MatchesWhoTable(float hb, int age, Sex sex, string expected)
        => SeverityBands.Classify(hb, age, sex).Severity.Should().Be(expected);

    /// <summary>
    /// Ровно тот случай, ради которого делался P0 №10: один и тот же Hb даёт
    /// разный вердикт в зависимости от группы. Раньше всем выдавалась детская
    /// шкала, и оба этих пациента получали «Non-Anemic».
    /// </summary>
    [Fact]
    public void Classify_SameHemoglobinDiffersByGroup()
    {
        const float hb = 11.5f;

        SeverityBands.Classify(hb, 3, Sex.Female).Severity.Should().Be(NonAnemic, "для ребёнка 11.5 — норма");
        SeverityBands.Classify(hb, 30, Sex.Female).Severity.Should().Be(Mild, "для взрослой женщины это лёгкая анемия");
        SeverityBands.Classify(hb, 30, Sex.Male).Severity.Should().Be(Mild, "для взрослого мужчины тоже");
    }

    /// <summary>
    /// Взрослый мужчина с Hb 12.5: по женской шкале норма, по мужской — анемия.
    /// Именно на этой разнице в обучающей выборке 15 пациентов из 215 сменили
    /// класс, и все в сторону «анемия» (см. AnemiaScanML/README.md).
    /// </summary>
    [Fact]
    public void Classify_MaleThresholdIsHigherThanFemale()
    {
        const float hb = 12.5f;

        SeverityBands.Classify(hb, 40, Sex.Female).Severity.Should().Be(NonAnemic);
        SeverityBands.Classify(hb, 40, Sex.Male).Severity.Should().Be(Mild);
    }

    [Theory]
    [InlineData(null, Sex.Female)]
    [InlineData(30, null)]
    [InlineData(null, null)]
    public void Classify_FallsBackToStrictestWhenProfileIncomplete(int? age, Sex? sex)
    {
        var assessment = SeverityBands.Classify(12.5f, age, sex);

        assessment.Reference.Should().Be(SeverityReference.AssumedStrictest);
        assessment.DemographicsComplete.Should().BeFalse();
        // Строгая шкала = мужская: безопаснее перестраховаться и отправить
        // к врачу, чем ложно успокоить.
        assessment.Severity.Should().Be(Mild);
    }

    [Theory]
    [InlineData(2, Sex.Female, SeverityReference.PreSchoolChild)]
    [InlineData(8, Sex.Male, SeverityReference.SchoolChild)]
    [InlineData(13, Sex.Female, SeverityReference.Teen)]
    [InlineData(15, Sex.Female, SeverityReference.AdultFemale)]
    [InlineData(15, Sex.Male, SeverityReference.AdultMale)]
    [InlineData(80, Sex.Male, SeverityReference.AdultMale)]
    public void ResolveReference_PicksExpectedScale(int age, Sex sex, SeverityReference expected)
        => SeverityBands.ResolveReference(age, sex).Should().Be(expected);

    [Fact]
    public void ResolveReference_IgnoresSexBelowAdultAge()
    {
        // До 15 лет шкала одинаковая для обоих полов — различать начинает ВОЗ
        // только у взрослых.
        SeverityBands.ResolveReference(10, Sex.Male)
            .Should().Be(SeverityBands.ResolveReference(10, Sex.Female));
    }

    [Theory]
    [InlineData(SeverityReference.PreSchoolChild, 11.0f)]
    [InlineData(SeverityReference.SchoolChild, 11.5f)]
    [InlineData(SeverityReference.Teen, 12.0f)]
    [InlineData(SeverityReference.AdultFemale, 12.0f)]
    [InlineData(SeverityReference.AdultMale, 13.0f)]
    public void NonAnemicThreshold_MatchesWho(SeverityReference reference, float expected)
        => SeverityBands.NonAnemicThreshold(reference).Should().Be(expected);
}
