using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.HealthChecks;

/// <summary>
/// Пингует MongoDB командой <c>{ ping: 1 }</c>. Используется в readiness-пробе
/// <c>/health/ready</c>: без базы приложение живо, но бесполезно — деплой и
/// мониторинг должны это видеть (P0 №5 в docs/plans/MVP_PLAN.md).
/// </summary>
public class MongoDbHealthCheck(IMongoDatabase database, ILogger<MongoDbHealthCheck> logger) : IHealthCheck
{
    /// <summary>Пинг дольше этого таймаута считаем недоступной базой.</summary>
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PingTimeout);

        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1),
                cancellationToken: timeout.Token);

            return HealthCheckResult.Healthy($"MongoDB '{database.DatabaseNamespace.DatabaseName}' доступна");
        }
        catch (Exception e)
        {
            logger.LogError(e, "MongoDB health check failed");
            return HealthCheckResult.Unhealthy("MongoDB недоступна", e);
        }
    }
}
