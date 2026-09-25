using System.Text.Json;
using System.Text.Json.Serialization;

using AnemiaScanApi.Infrastructure.HealthChecks;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AnemiaScanApi.Extensions;

/// <summary>
/// Health-пробы (P0 №5 в docs/plans/MVP_PLAN.md). Две штуки с разным смыслом:
/// <c>/health</c> — liveness (процесс жив, systemd/мониторингу этого достаточно,
/// чтобы решать про рестарт), <c>/health/ready</c> — readiness (база отвечает,
/// модели на диске). Обе анонимные: мониторинг ходит без JWT.
/// </summary>
public static class HealthExtensions
{
    /// <summary>Тег readiness-проверок — по нему фильтруется <c>/health/ready</c>.</summary>
    private const string ReadyTag = "ready";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IServiceCollection AddSasHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: [ReadyTag])
            .AddCheck<MlModelsHealthCheck>("ml-models", tags: [ReadyTag]);

        return services;
    }

    public static WebApplication MapSasHealthChecks(this WebApplication app)
    {
        // Liveness: ни одной проверки не выполняем — важен сам факт, что процесс
        // отвечает. Иначе недоступная Mongo провоцировала бы бесконечный рестарт.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponseAsync
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// JSON вместо дефолтного plain-text: мониторингу нужен разбор по проверкам.
    /// Текст исключения наружу не отдаём — он уходит в лог (см. health-check'и).
    /// </summary>
    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1)
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, ResponseJsonOptions));
    }
}
