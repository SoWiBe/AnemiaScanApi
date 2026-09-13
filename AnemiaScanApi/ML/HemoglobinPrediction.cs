namespace AnemiaScanApi.ML;

/// <summary>Результат CIELab-регрессии Hb: значение + категория тяжести.</summary>
public sealed record HemoglobinPrediction(float HemoglobinLevel, string Severity);
