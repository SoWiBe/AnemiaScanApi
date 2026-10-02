namespace AnemiaScanApi.ML;

/// <summary>
/// Результат CIELab-регрессии Hb — только число.
///
/// Категория тяжести сюда намеренно не входит: чтобы её посчитать, нужны пол и
/// возраст пациента (P0 №10), а модель о пациенте ничего не знает. Severity
/// добавляется слоем выше, в AnemiaAnalysisService, где профиль уже доступен.
/// </summary>
public sealed record HemoglobinPrediction(float HemoglobinLevel);
