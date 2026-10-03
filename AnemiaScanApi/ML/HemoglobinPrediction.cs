namespace AnemiaScanApi.ML;

/// <summary>
/// Результат CIELab-регрессии Hb: число плюс то, насколько ему можно верить.
///
/// Категория тяжести сюда намеренно не входит: чтобы её посчитать, нужен пол
/// пациента (P0 №10), а регрессия о нём не знает. Severity добавляется слоем
/// выше, в AnemiaAnalysisService.
/// </summary>
/// <param name="HemoglobinLevel">Предсказанный Hb, г/дл.</param>
/// <param name="ModelVersion">Какая из регрессий посчитала число — пишется в скан.</param>
/// <param name="AgeInDomain">Возраст пациента попадает в обучающий диапазон выбранной модели.</param>
/// <param name="InputSegmented">
/// На входе вырезанная конъюнктива с прозрачным фоном, а не целый кадр.
/// Обе модели обучены на масках; по целому кадру экстрактор усредняет цвет
/// всего изображения, и признаки получаются из другого распределения.
/// </param>
public sealed record HemoglobinPrediction(
    float HemoglobinLevel,
    string ModelVersion,
    bool AgeInDomain,
    bool InputSegmented)
{
    /// <summary>
    /// Число Hb можно показывать как измерение только когда совпали и
    /// популяция, и формат входа.
    /// </summary>
    public bool InDomain => AgeInDomain && InputSegmented;
}
