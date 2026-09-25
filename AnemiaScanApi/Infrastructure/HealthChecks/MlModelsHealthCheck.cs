using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AnemiaScanApi.Infrastructure.HealthChecks;

/// <summary>
/// Проверяет, что файлы моделей на месте. Инференс тут намеренно не запускается:
/// прогон TF-классификатора стоит секунды CPU, и readiness-проба, которую дёргает
/// мониторинг, не должна конкурировать за процессор с реальными сканами.
/// Пути повторяют <see cref="Extensions.LLMExtensions"/>.
/// </summary>
public class MlModelsHealthCheck : IHealthCheck
{
    private static readonly string[] ModelPaths =
    [
        Path.Combine(AppContext.BaseDirectory, "LLM", "anemia_v10_more_aug.zip"),
        Path.Combine(AppContext.BaseDirectory, "ML", "hb_model.onnx")
    ];

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var missing = ModelPaths.Where(path => !File.Exists(path)).ToArray();

        return Task.FromResult(missing.Length == 0
            ? HealthCheckResult.Healthy("Файлы моделей на месте")
            : HealthCheckResult.Unhealthy($"Не найдены файлы моделей: {string.Join(", ", missing)}"));
    }
}
