using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.ML;

/// <summary>
/// Шкала ВОЗ, по которой оценён конкретный скан. Пишется в скан и отдаётся
/// клиенту, чтобы было видно, к какой группе отнесли пациента.
/// </summary>
public enum SeverityReference
{
    /// <summary>Дети 6-59 месяцев.</summary>
    PreSchoolChild,

    /// <summary>Дети 5-11 лет.</summary>
    SchoolChild,

    /// <summary>Подростки 12-14 лет.</summary>
    Teen,

    /// <summary>Женщины от 15 лет (небеременные).</summary>
    AdultFemale,

    /// <summary>Мужчины от 15 лет.</summary>
    AdultMale,

    /// <summary>
    /// В профиле не хватает пола или возраста. Применена самая строгая взрослая
    /// шкала — см. <see cref="SeverityBands"/>.
    /// </summary>
    AssumedStrictest
}

/// <summary>Результат оценки тяжести: категория плюс то, по какой шкале её получили.</summary>
/// <param name="DemographicsComplete">
/// false — пол или возраст в профиле отсутствуют, и оценка сделана по
/// умолчанию. Клиенту стоит попросить пользователя дозаполнить профиль.
/// </param>
public sealed record SeverityAssessment(string Severity, SeverityReference Reference, bool DemographicsComplete);

/// <summary>
/// Категория тяжести анемии по Hb, шкала ВОЗ (P0 №10 в docs/plans/MVP_PLAN.md).
///
/// ЧТО БЫЛО НЕ ТАК: раньше здесь была одна шкала — для детей 6-59 месяцев
/// (7/10/11), и она применялась ко всем. Взрослой женщине с Hb 11.5
/// приложение отвечало «Non-Anemic», хотя по её шкале это лёгкая анемия;
/// мужчине с Hb 12.5 — то же самое. Ошибка односторонняя: занижала тяжесть,
/// то есть ложно успокаивала.
///
/// Та же правка на стороне обучения показала масштаб: при переходе с общего
/// порога 12.0 на возрастно-половые 15 пациентов из 215 сменили класс, и все
/// пятнадцать — из «здоров» в «анемия» (см. AnemiaScanML/README.md).
///
/// ИСТОЧНИК: WHO, Haemoglobin concentrations for the diagnosis of anaemia and
/// assessment of severity (2011), таблица по г/дл. Беременные вынесены
/// отдельной строкой в оригинале, но в профиле нет признака беременности,
/// поэтому эта шкала здесь не реализована — см. комментарий к
/// <see cref="Classify"/>.
/// </summary>
public static class SeverityBands
{
    public const string Severe = "Severe";
    public const string Moderate = "Moderate";
    public const string Mild = "Mild";
    public const string NonAnemic = "Non-Anemic";

    /// <summary>Возраст, с которого применяются взрослые шкалы.</summary>
    public const int AdultAgeYears = 15;

    /// <summary>
    /// Границы одной строки таблицы ВОЗ. Значение относится к категории, если
    /// оно меньше соответствующей границы; всё, что не ниже
    /// <paramref name="NonAnemicMin"/>, — норма.
    /// </summary>
    private readonly record struct Bands(float SevereMax, float ModerateMax, float NonAnemicMin)
    {
        public string Classify(float hemoglobin) => hemoglobin switch
        {
            var hb when hb < SevereMax => Severe,
            var hb when hb < ModerateMax => Moderate,
            var hb when hb < NonAnemicMin => Mild,
            _ => NonAnemic
        };
    }

    private static Bands For(SeverityReference reference) => reference switch
    {
        SeverityReference.PreSchoolChild => new Bands(7.0f, 10.0f, 11.0f),
        SeverityReference.SchoolChild => new Bands(8.0f, 11.0f, 11.5f),
        SeverityReference.Teen => new Bands(8.0f, 11.0f, 12.0f),
        SeverityReference.AdultFemale => new Bands(8.0f, 11.0f, 12.0f),
        SeverityReference.AdultMale => new Bands(8.0f, 11.0f, 13.0f),

        // Пол или возраст неизвестны. Берём мужскую взрослую шкалу — у неё самый
        // высокий порог нормы, значит пациент скорее попадёт в анемичные, чем
        // будет ошибочно признан здоровым. Для скрининга это безопасная сторона
        // ошибки: ложная тревога стоит человеку анализа крови, пропуск — дороже.
        SeverityReference.AssumedStrictest => new Bands(8.0f, 11.0f, 13.0f),

        _ => throw new ArgumentOutOfRangeException(nameof(reference), reference, null)
    };

    /// <summary>
    /// Шкала по демографии пациента.
    ///
    /// Беременность в профиле не хранится, поэтому беременные попадают под
    /// женскую взрослую шкалу (порог нормы 12.0 вместо 11.0 по ВОЗ) — то есть
    /// оценка получается строже реальной. Это опять же безопасная сторона:
    /// завысить тяжесть, а не занизить. Когда в профиль добавится признак
    /// беременности (P1 в плане, пункт про PickAudience), сюда добавится строка.
    /// </summary>
    public static SeverityReference ResolveReference(int? ageYears, Sex? sex)
    {
        if (ageYears is not { } age || age < 0) return SeverityReference.AssumedStrictest;

        if (age < 5) return SeverityReference.PreSchoolChild;
        if (age < 12) return SeverityReference.SchoolChild;
        if (age < AdultAgeYears) return SeverityReference.Teen;

        return sex switch
        {
            Sex.Male => SeverityReference.AdultMale,
            Sex.Female => SeverityReference.AdultFemale,
            _ => SeverityReference.AssumedStrictest
        };
    }

    /// <summary>
    /// Оценивает тяжесть по Hb и демографии.
    /// </summary>
    public static SeverityAssessment Classify(float hemoglobin, int? ageYears, Sex? sex)
    {
        var reference = ResolveReference(ageYears, sex);

        return new SeverityAssessment(
            For(reference).Classify(hemoglobin),
            reference,
            DemographicsComplete: reference != SeverityReference.AssumedStrictest);
    }

    /// <summary>Порог нормы для группы — то значение Hb, ниже которого начинается анемия.</summary>
    public static float NonAnemicThreshold(SeverityReference reference) => For(reference).NonAnemicMin;
}
