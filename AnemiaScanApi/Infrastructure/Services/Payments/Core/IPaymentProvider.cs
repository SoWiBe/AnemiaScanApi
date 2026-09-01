using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Payments;

namespace AnemiaScanApi.Infrastructure.Services.Payments.Core;

/// <summary>
/// One payment rail. Implementations are resolved by <see cref="Type"/>, so adding a rail
/// never touches the facade, the controller or the reconciliation worker.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Rail this implementation handles.
    /// </summary>
    PaymentProviderType Type { get; }

    /// <summary>
    /// Fills in the rail-specific fields of a freshly built intent (reference key, amount, TTL) and
    /// returns the deep link / QR payload for the client. Does not persist — the facade does that.
    /// </summary>
    PaymentInitiationResponse Create(PaymentIntent intent, Course course);

    /// <summary>
    /// Renders the client payload for an intent that already exists, without touching its state.
    /// Lets the facade hand a still-payable intent back instead of minting a second one.
    /// </summary>
    PaymentInitiationResponse Describe(PaymentIntent intent, Course course);

    /// <summary>
    /// Asks the rail whether the intent has settled. Pure query — must not mutate or persist
    /// the intent; the facade owns state transitions.
    /// </summary>
    Task<ProviderStatusResult> GetStatusAsync(PaymentIntent intent, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a provider observed about an intent.
/// </summary>
/// <param name="Status">Status as the rail sees it.</param>
/// <param name="TransactionSignature">Signature of the settling transaction, when confirmed.</param>
public record ProviderStatusResult(PaymentStatus Status, string? TransactionSignature = null);
