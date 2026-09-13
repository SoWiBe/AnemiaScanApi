namespace AnemiaScanApi.ML;

/// <summary>
/// Категория тяжести анемии по предсказанному Hb.
///
/// Границы — стандартная классификация ВОЗ по Hb для детей 6-59 месяцев
/// (г/дл): Severe &lt;7.0, Moderate 7.0-9.9, Mild 10.0-10.9, Non-Anemic &gt;=11.0.
/// Возрастной диапазон CP-AnemiC (age_months 6-60) и наблюдаемые в
/// Anemia_Data_Collection_Sheet.xlsx границы (Non-Anemic&gt;=11, Mild
/// 10-10.93, Moderate 7-9.98, Severe&lt;=6.9 — см. anemia-machine-learning/
/// model/model_card.json) совпадают с этой шкалой с точностью до выборочного
/// шума на краях категорий.
///
/// ВАЖНО: округлённые точки (7/10/11) — интерпретация, а не дословная цитата
/// источника данных (в датасете нет явно указанной формулы, только уже
/// размеченная колонка Severity). Сверить с оригинальной статьёй CP-AnemiC
/// перед тем как использовать эту категоризацию в финальных клинических
/// выводах — см. PLAN_pretraining_and_api_integration.md, Этап E.
/// </summary>
public static class SeverityBands
{
    public const float SevereMax = 7.0f;
    public const float ModerateMax = 10.0f;
    public const float NonAnemicMin = 11.0f;

    public static string Classify(float hemoglobin) => hemoglobin switch
    {
        < SevereMax => "Severe",
        < ModerateMax => "Moderate",
        < NonAnemicMin => "Mild",
        _ => "Non-Anemic",
    };
}
