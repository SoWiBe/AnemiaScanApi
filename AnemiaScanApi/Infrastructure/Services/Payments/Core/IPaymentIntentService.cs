using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Payments;

namespace AnemiaScanApi.Infrastructure.Services.Payments.Core;

/// <summary>
/// Facade over the payment providers. Owns intent persistence, state transitions and the
/// enrollment that a confirmed payment unlocks.
/// </summary>
public interface IPaymentIntentService
{
    /// <summary>
    /// Creates (or reuses) a payable intent for a course.
    /// Throws 409 if the course is free or already paid for by this user.
    /// </summary>
    Task<PaymentInitiationResponse> CreateCourseIntentAsync(
        Guid userId,
        string courseSlug,
        PaymentProviderType provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the provider and persists any resulting transition. On the first observed
    /// confirmation the enrollment is created; repeat calls return the same enrollment id.
    /// </summary>
    Task<PaymentStatusResponse> GetStatusAsync(Guid userId, Guid intentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drives a single intent to its terminal state without an ownership check — used by the
    /// reconciliation worker for users who closed the app mid-payment.
    /// </summary>
    Task<PaymentStatus> ReconcileAsync(PaymentIntent intent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mock-mode only: forces an intent to Confirmed so the client can be tested without waiting.
    /// Throws 404 when the Solana provider is not running in mock mode.
    /// </summary>
    Task<PaymentStatusResponse> MockConfirmAsync(Guid userId, Guid intentId, CancellationToken cancellationToken = default);
}
