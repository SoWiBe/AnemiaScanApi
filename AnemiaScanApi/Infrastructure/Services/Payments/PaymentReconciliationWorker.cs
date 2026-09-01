using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;

namespace AnemiaScanApi.Services.Payments;

/// <summary>
/// Drives Pending intents to a terminal state without the client.
///
/// The client poll covers the happy path, but a user who closes the app right after paying would
/// otherwise leave a confirmed on-chain transfer with no enrollment behind it. This sweep closes
/// that gap and expires abandoned intents so <c>CreateCourseIntentAsync</c> stops reusing them.
/// </summary>
public class PaymentReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentReconciliationWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Payment reconciliation worker started, sweeping every {Interval}", SweepInterval);

        using var timer = new PeriodicTimer(SweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad sweep kill the worker — the next tick retries.
                logger.LogError(ex, "Payment reconciliation sweep failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }

        logger.LogInformation("Payment reconciliation worker stopped");
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        // Repositories and the facade are scoped — the worker is a singleton, so each sweep
        // gets its own scope.
        using var scope = scopeFactory.CreateScope();

        var intentsRepository = scope.ServiceProvider.GetRequiredService<IPaymentIntentsRepository>();
        var intentService = scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();

        var pending = (await intentsRepository.GetPendingAsync(BatchSize, cancellationToken)).ToList();
        if (pending.Count == 0) return;

        var settled = 0;
        foreach (var intent in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var status = await intentService.ReconcileAsync(intent, cancellationToken);
                if (status != Common.Enums.PaymentStatus.Pending) settled++;
            }
            catch (Exception ex)
            {
                // One unreachable provider must not stall the rest of the batch.
                logger.LogError(ex, "Failed to reconcile payment intent {IntentId}", intent.Id);
            }
        }

        logger.LogInformation("Reconciliation sweep: {Checked} pending intents checked, {Settled} settled",
            pending.Count, settled);
    }
}
