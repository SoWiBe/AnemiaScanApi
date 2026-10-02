using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.ML;

using FluentAssertions;

namespace AnemiaScanApi.Tests.ML;

/// <summary>
/// Единый вердикт из двух независимых предсказаний (P0 №11 в docs/plans/MVP_PLAN.md).
///
/// Модели не связаны между собой: TF-классификатор обучен на взрослых
/// (Eyes-defy-anemia), CIELab-регрессия — на детях до 5 лет (CP-AnemiC).
/// Поэтому вердикт отдаётся классификатору, а расхождение показывается как
/// неуверенность, а не скрывается.
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
        // классификатором: он измерен и обучен на нужной популяции.
        var opposite = classifierSaysAnemic ? Healthy() : Anemic();

        VerdictReconciler.Reconcile(classifierSaysAnemic, opposite, ageYears: 30)
            .Outcome.Should().Be(expected);
    }

    [Fact]
    public void Reconcile_AgreeWhenBothSayAnemic()
        => VerdictReconciler.Reconcile(true, Anemic(), 30)
            .Agreement.Should().Be(VerdictAgreement.Agree);

    [Fact]
    public void Reconcile_AgreeWhenBothSayHealthy()
        => VerdictReconciler.Reconcile(false, Healthy(), 30)
            .Agreement.Should().Be(VerdictAgreement.Agree);

    [Theory]
    [InlineData(true, SeverityBands.NonAnemic)]
    [InlineData(false, SeverityBands.Mild)]
    [InlineData(false, SeverityBands.Moderate)]
    [InlineData(false, SeverityBands.Severe)]
    public void Reconcile_DisagreeWhenPathsConflict(bool classifierSaysAnemic, string severity)
        => VerdictReconciler.Reconcile(classifierSaysAnemic, Severity(severity), 30)
            .Agreement.Should().Be(VerdictAgreement.Disagree);

    [Fact]
    public void Reconcile_UnavailableWhenRegressionFailed()
    {
        var verdict = VerdictReconciler.Reconcile(true, severity: null, ageYears: 30);

        verdict.Agreement.Should().Be(VerdictAgreement.Unavailable);
        verdict.Outcome.Should().Be(Sick.Anemia, "отказ второго пути не должен лишать пользователя вердикта");
        verdict.HemoglobinInDomain.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(30, false)]
    [InlineData(80, false)]
    public void Reconcile_HemoglobinInDomainOnlyForYoungChildren(int age, bool expected)
    {
        // CIELab-регрессия обучена на CP-AnemiC: дети 6-59 месяцев. Для всех
        // остальных её число — экстраполяция, и показывать его как измерение нельзя.
        VerdictReconciler.Reconcile(true, Anemic(), age)
            .HemoglobinInDomain.Should().Be(expected);
    }

    [Fact]
    public void Reconcile_HemoglobinNotInDomainWhenAgeUnknown()
    {
        // Без возраста нельзя утверждать, что пациент в области определения —
        // поэтому не утверждаем.
        VerdictReconciler.Reconcile(true, Anemic(), ageYears: null)
            .HemoglobinInDomain.Should().BeFalse();
    }

    /// <summary>
    /// Самый важный для продукта случай: взрослый, модели разошлись.
    /// Вердикт есть, но он помечен как неуверенный, и число Hb не считается
    /// достоверным — ровно та ситуация, которую экран результата должен
    /// показывать как «тем более стоит сдать анализ крови».
    /// </summary>
    [Fact]
    public void Reconcile_AdultWithConflictingPaths()
    {
        var verdict = VerdictReconciler.Reconcile(classifierSaysAnemic: true, Healthy(), ageYears: 35);

        verdict.Outcome.Should().Be(Sick.Anemia);
        verdict.Agreement.Should().Be(VerdictAgreement.Disagree);
        verdict.HemoglobinInDomain.Should().BeFalse();
    }
}
