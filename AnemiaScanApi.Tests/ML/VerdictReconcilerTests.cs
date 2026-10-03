using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.ML;

using FluentAssertions;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Единый вердикт из двух независимых предсказаний (P0 №11 в docs/plans/MVP_PLAN.md).
///
/// Вердикт отдаётся классификатору, расхождение с регрессией показывается как
/// неуверенность, а достоверность числа Hb приходит из самого предсказания —
/// её определяют возраст и формат входа (см. HemoglobinPredictionServiceTests).
/// </summary>
public class VerdictReconcilerTests
{
    private static SeverityAssessment Severity(string label) =>
        new(label, SeverityReference.AdultFemale, DemographicsComplete: true);

    private static SeverityAssessment Anemic() => Severity(SeverityBands.Mild);
    private static SeverityAssessment Healthy() => Severity(SeverityBands.NonAnemic);

    [Theory]
    [InlineData(true, Sick.Anemia)]
    [InlineData(false, Sick.Healthy)]
    public void Reconcile_OutcomeAlwaysComesFromClassifier(bool classifierSaysAnemic, Sick expected)
    {
        // Даже когда регрессия говорит обратное, вердикт остаётся за
        // классификатором: его качество измерено именно как классификатора.
        var opposite = classifierSaysAnemic ? Healthy() : Anemic();

        VerdictReconciler.Reconcile(classifierSaysAnemic, opposite, hemoglobinInDomain: true)
            .Outcome.Should().Be(expected);
    }

    [Fact]
    public void Reconcile_AgreeWhenBothSayAnemic()
        => VerdictReconciler.Reconcile(true, Anemic(), hemoglobinInDomain: true)
            .Agreement.Should().Be(VerdictAgreement.Agree);

    [Fact]
    public void Reconcile_AgreeWhenBothSayHealthy()
        => VerdictReconciler.Reconcile(false, Healthy(), hemoglobinInDomain: true)
            .Agreement.Should().Be(VerdictAgreement.Agree);

    [Theory]
    [InlineData(true, SeverityBands.NonAnemic)]
    [InlineData(false, SeverityBands.Mild)]
    [InlineData(false, SeverityBands.Moderate)]
    [InlineData(false, SeverityBands.Severe)]
    public void Reconcile_DisagreeWhenPathsConflict(bool classifierSaysAnemic, string severity)
        => VerdictReconciler.Reconcile(classifierSaysAnemic, Severity(severity), hemoglobinInDomain: true)
            .Agreement.Should().Be(VerdictAgreement.Disagree);

    [Fact]
    public void Reconcile_UnavailableWhenRegressionFailed()
    {
        var verdict = VerdictReconciler.Reconcile(true, severity: null, hemoglobinInDomain: true);

        verdict.Agreement.Should().Be(VerdictAgreement.Unavailable);
        verdict.Outcome.Should().Be(Sick.Anemia, "отказ второго пути не должен лишать пользователя вердикта");
        verdict.HemoglobinInDomain.Should().BeFalse("нет числа — нечему быть достоверным");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reconcile_PassesHemoglobinDomainThrough(bool inDomain)
        => VerdictReconciler.Reconcile(true, Anemic(), inDomain)
            .HemoglobinInDomain.Should().Be(inDomain);

    /// <summary>
    /// Самый важный для продукта случай: модели разошлись, а число Hb ещё и
    /// недостоверно (например, прислан целый кадр, а не вырезанная конъюнктива).
    /// Вердикт есть, но экран результата должен показать его с оговоркой —
    /// «тем более стоит сдать анализ крови».
    /// </summary>
    [Fact]
    public void Reconcile_ConflictWithUntrustedHemoglobin()
    {
        var verdict = VerdictReconciler.Reconcile(classifierSaysAnemic: true, Healthy(), hemoglobinInDomain: false);

        verdict.Outcome.Should().Be(Sick.Anemia);
        verdict.Agreement.Should().Be(VerdictAgreement.Disagree);
        verdict.HemoglobinInDomain.Should().BeFalse();
    }
}
