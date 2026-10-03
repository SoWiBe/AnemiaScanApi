namespace AnemiaScanApi.ML;

/// <summary>
/// CIELab-регрессия Hb вместе со своей областью определения.
///
/// В API две такие модели, обученные на разных популяциях: детская (CP-AnemiC,
/// 6-59 месяцев) и взрослая (Eyes-defy-anemia, 19-88 лет). Вне своего возраста
/// модель выдаёт не измерение, а экстраполяцию, поэтому область определения —
/// часть модели, а не знание, разбросанное по вызывающему коду.
/// </summary>
public interface IHemoglobinModel
{
    /// <summary>Версия, которая пишется в скан (<c>AnemiaScan.ModelVersion</c>).</summary>
    string Version { get; }

    /// <summary>Возрастной диапазон обучающей выборки, в годах, включительно.</summary>
    AgeRange TrainedAgeRange { get; }

    float Predict(CielabFeatures features);
}

/// <summary>Возрастной диапазон в полных годах, обе границы включительно.</summary>
public readonly record struct AgeRange(int MinYears, int MaxYears)
{
    public bool Contains(int ageYears) => ageYears >= MinYears && ageYears <= MaxYears;

    public override string ToString() => $"{MinYears}-{MaxYears} лет";
}
