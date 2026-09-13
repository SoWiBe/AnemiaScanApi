namespace AnemiaScanApi.Tests.Fixtures;

/// <summary>
/// Одна эталонная запись из anemia-machine-learning/data/cpanemic_clean_v1_features.csv:
/// L/a/b и предсказанный Hb, посчитанные Python-пайплайном (features.py +
/// model/hb_model.joblib). Сгенерировано отдельным скриптом на этапе D
/// (см. PLAN_pretraining_and_api_integration.md) — не редактировать вручную,
/// перегенерировать вместе с conjunctiva_golden.json при переобучении модели.
/// </summary>
public sealed class ConjunctivaGoldenRecord
{
    public string ImageId { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public double Hb { get; set; }
    public double L { get; set; }
    public double A { get; set; }
    public double B { get; set; }
    public double PredictedHb { get; set; }

    public override string ToString() => $"{ImageId} ({Severity}, Hb={Hb})";
}
