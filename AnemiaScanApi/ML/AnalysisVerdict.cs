using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.ML;

/// <summary>Сошлись ли два независимых пути предсказания на одном снимке.</summary>
public enum VerdictAgreement
{
    /// <summary>Обе модели согласны.</summary>
    Agree,

    /// <summary>Модели разошлись. Не ошибка, а сигнал неуверенности.</summary>
    Disagree,

    /// <summary>CIELab-регрессия не посчиталась — сравнивать не с чем.</summary>
    Unavailable
}

/// <summary>
/// Единый вердикт по снимку (P0 №11 в docs/plans/MVP_PLAN.md).
/// </summary>
/// <param name="Outcome">
/// Единственный вердикт. Берётся у TF-классификатора — см.
/// <see cref="VerdictReconciler"/> о том, почему именно у него.
/// </param>
/// <param name="Agreement">Согласие двух путей. Расхождение показываем, а не прячем.</param>
/// <param name="HemoglobinInDomain">
/// Получено ли число Hb моделью, обученной на популяции этого пациента.
/// False — цифру показывать как измерение нельзя.
/// </param>
public sealed record AnalysisVerdict(Sick Outcome, VerdictAgreement Agreement, bool HemoglobinInDomain);

/// <summary>
/// Сводит два независимых предсказания в один вердикт (P0 №11).
///
/// ЧТО ЗА ДВЕ МОДЕЛИ. Они не помогают друг другу и не дообучают друг друга —
/// это два отдельных мнения об одной фотографии:
///
/// - <b>TF-классификатор</b> (Inception + LightGBM): метка Low_Hb/High_Hb.
///   Обучен на Eyes-defy-anemia — 215 <b>взрослых</b> 19-88 лет.
///   Измерен: sensitivity 72.2%, specificity 77.6% на out-of-fold
///   кросс-валидации (см. AnemiaScanML/README.md).
/// - <b>CIELab-регрессия</b> (3 признака L/a/b → GradientBoosting): число Hb.
///   Обучена на CP-AnemiC — 394 записи, <b>дети 6-59 месяцев</b>.
///   MAE 1.52 г/дл, r = 0.387 (ML/model_card.json).
///
/// ПОЧЕМУ ВЕРДИКТ ОТДАЁТСЯ КЛАССИФИКАТОРУ. Аудитория приложения — взрослые.
/// Классификатор на них и обучен, и его качество измерено. Регрессия обучена на
/// детях до 5 лет, то есть для взрослого пациента работает вне своей области
/// определения, и её число Hb — не измерение, а экстраполяция. Отдавать ей
/// вердикт было бы хуже обоснованно.
///
/// ПОЧЕМУ РАСХОЖДЕНИЕ НЕ ПРЯЧЕТСЯ. Когда два независимых пути расходятся, это
/// информация о неуверенности. Для скрининга честный вывод из расхождения —
/// «тем более стоит сдать анализ крови», а не «покажем одно из мнений и сделаем
/// вид, что всё однозначно».
/// </summary>
public static class VerdictReconciler
{
    /// <summary>
    /// Верхняя граница возраста, на котором обучалась CIELab-регрессия.
    /// CP-AnemiC — 6-59 месяцев, то есть примерно до 5 лет.
    /// </summary>
    private const int CielabTrainedUpToAgeYears = 5;

    /// <param name="classifierSaysAnemic">Решение TF-классификатора (метка Low_Hb).</param>
    /// <param name="severity">
    /// Оценка тяжести по числу Hb, либо null, если регрессия не посчиталась.
    /// </param>
    /// <param name="ageYears">Возраст пациента, либо null, если профиль неполон.</param>
    public static AnalysisVerdict Reconcile(bool classifierSaysAnemic, SeverityAssessment? severity, int? ageYears)
    {
        var outcome = classifierSaysAnemic ? Sick.Anemia : Sick.Healthy;

        if (severity is null)
            return new AnalysisVerdict(outcome, VerdictAgreement.Unavailable, HemoglobinInDomain: false);

        var regressionSaysAnemic = severity.Severity != SeverityBands.NonAnemic;

        var agreement = regressionSaysAnemic == classifierSaysAnemic
            ? VerdictAgreement.Agree
            : VerdictAgreement.Disagree;

        // Возраст неизвестен — не утверждаем, что пациент в области определения.
        var inDomain = ageYears is { } and >= 0 and < CielabTrainedUpToAgeYears;

        return new AnalysisVerdict(outcome, agreement, inDomain);
    }
}
