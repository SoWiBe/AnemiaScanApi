namespace AnemiaScanApi.Common.Responses;

/// <param name="ScanId">Скан, к которому привязан анализ.</param>
/// <param name="LabHemoglobin">Сохранённый Hb из анализа крови, г/дл.</param>
/// <param name="LabMeasuredAt">Дата сдачи крови (без времени).</param>
public record LabHemoglobinResponse(Guid ScanId, double LabHemoglobin, DateTime LabMeasuredAt);
