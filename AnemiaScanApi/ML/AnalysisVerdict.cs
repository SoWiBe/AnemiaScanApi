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
/// - <b>CIELab-регрессия</b> (3 признака L/a/b → бустинг): число Hb. Их две,
///   выбирается по возрасту (см. HemoglobinPredictionService): детская
///   (CP-AnemiC, 6-59 месяцев, MAE 1.52, r 0.387) и взрослая
///   (Eyes-defy-anemia, 19-88 лет, MAE 1.38, r 0.686).
///
/// ПОЧЕМУ ВЕРДИКТ ОТДАЁТСЯ КЛАССИФИКАТОРУ. Его качество измерено именно как
/// классификатора — чувствительность и специфичность. У регрессии измерена
/// ошибка в г/дл, а не то, насколько верно она отличает анемию от нормы; плюс
/// её число зависит от того, вырезана ли на снимке конъюнктива. Отдавать
/// вердикт ей было бы хуже обоснованно.
///
/// ПОЧЕМУ РАСХОЖДЕНИЕ НЕ ПРЯЧЕТСЯ. Когда два независимых пути расходятся, это
/// информация о неуверенности. Для скрининга честный вывод из расхождения —
/// «тем более стоит сдать анализ крови», а не «покажем одно из мнений и сделаем
/// вид, что всё однозначно».
/// </summary>
public static class VerdictReconciler
{
    /// <param name="classifierSaysAnemic">Решение TF-классификатора (метка Low_Hb).</param>
    /// <param name="severity">
    /// Оценка тяжести по числу Hb, либо null, если регрессия не посчиталась.
    /// </param>
    /// <param name="hemoglobinInDomain">
    /// Достоверно ли число Hb: совпали и возраст с обучающим диапазоном модели,
    /// и формат входа (вырезанная конъюнктива) — см. <see cref="HemoglobinPrediction.InDomain"/>.
    /// </param>
    public static AnalysisVerdict Reconcile(bool classifierSaysAnemic, SeverityAssessment? severity,
        bool hemoglobinInDomain)
    {
        var outcome = classifierSaysAnemic ? Sick.Anemia : Sick.Healthy;

        if (severity is null)
            return new AnalysisVerdict(outcome, VerdictAgreement.Unavailable, HemoglobinInDomain: false);

        var regressionSaysAnemic = severity.Severity != SeverityBands.NonAnemic;

        var agreement = regressionSaysAnemic == classifierSaysAnemic
            ? VerdictAgreement.Agree
            : VerdictAgreement.Disagree;

        return new AnalysisVerdict(outcome, agreement, hemoglobinInDomain);
    }
}
