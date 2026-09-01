using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;
using AnemiaScanApi.Services;
using AnemiaScanApi.Services.Payments;
using AnemiaScanApi.Settings;

namespace AnemiaScanApi.Extensions;

/// <summary>
/// Wiring for the payment layer (Phase 2). Kept out of <see cref="ServicesExtensions"/> so the
/// whole rail — providers, facade, gate, worker — can be reasoned about (or disabled) in one place.
/// </summary>
public static class PaymentsExtensions
{
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SolanaSettings>(configuration.GetSection("Solana"));

        // Injected rather than DateTime.UtcNow so TTL/auto-confirm timing is testable.
        services.TryAddTimeProvider();

        services.AddScoped<IPaymentIntentsRepository, PaymentIntentsRepository>();

        // Both rails are registered; the facade picks one by PaymentProviderType.
        services.AddScoped<IPaymentProvider, SolanaPaymentProvider>();
        services.AddScoped<IPaymentProvider, KaspiPaymentProvider>();

        services.AddScoped<IPaymentIntentService, PaymentIntentService>();
        services.AddScoped<ICourseEntitlementService, CourseEntitlementService>();

        services.AddHostedService<PaymentReconciliationWorker>();

        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(TimeProvider))) return;
        services.AddSingleton(TimeProvider.System);
    }
}
