using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Middleware;
using AnemiaScanApi.Settings;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Extensions;

/// <summary>
/// Rate-limit на два места, где отсутствие лимита стоит денег или процессора:
/// анализ снимка и отправка email-кодов (P0 №6 в docs/plans/MVP_PLAN.md).
/// Остальные эндпоинты намеренно без лимита — на MVP это лишний риск
/// заблокировать живого пользователя.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddSasRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitSettings>(configuration.GetSection("RateLimiting"));

        services.AddRateLimiter(options =>
        {
            var settings = configuration.GetSection("RateLimiting").Get<RateLimitSettings>() ?? new RateLimitSettings();

            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

            // Партиция — пользователь из JWT: лимит на аккаунт, а не на IP, иначе
            // вся мобильная сеть оператора за одним NAT делила бы один лимит.
            options.AddPolicy(RateLimitPolicies.AnemiaPrediction, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserPartitionKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PredictionPermitLimit,
                        Window = TimeSpan.FromMinutes(settings.PredictionWindowMinutes),
                        QueueLimit = 0
                    }));

            // Эндпоинты отправки кода анонимные, партиционировать можно только по IP.
            // За nginx корректный IP появится после ForwardedHeaders (P1 в плане).
            options.AddPolicy(RateLimitPolicies.EmailCodes, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetIpPartitionKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.EmailCodePermitLimit,
                        Window = TimeSpan.FromMinutes(settings.EmailCodeWindowMinutes),
                        QueueLimit = 0
                    }));

            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    private static string GetUserPartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId) ? GetIpPartitionKey(context) : $"user:{userId}";
    }

    private static string GetIpPartitionKey(HttpContext context)
        => $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    /// <summary>
    /// Отдаём тот же <see cref="ExceptionResponse"/>, что и SASMiddleware, чтобы
    /// клиенту не пришлось разбирать второй формат ошибки.
    /// </summary>
    private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }

        context.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitingExtensions).FullName!);
        logger.LogWarning("Rate limit hit on {Path}", context.HttpContext.Request.Path);

        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            new ExceptionResponse(HttpStatusCode.TooManyRequests,
                "Слишком много запросов. Попробуйте позже."),
            cancellationToken));
    }
}
