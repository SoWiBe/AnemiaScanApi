using System.Globalization;
using System.Security.Cryptography;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Payments;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;
using AnemiaScanApi.Settings;
using AnemiaScanApi.Utils;

using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Services.Payments;

/// <summary>
/// Solana Pay rail.
///
/// <para><b>Creation is real in both modes</b> — the emitted <c>solana:</c> URL is a valid Solana Pay
/// transfer request that Phantom/Solflare will open, and the reference key is a genuine random
/// 32-byte key.</para>
///
/// <para><b>Confirmation is what mock mode replaces.</b> With <see cref="SolanaSettings.UseMock"/> on,
/// an intent reports itself confirmed <see cref="SolanaSettings.MockAutoConfirmSeconds"/> after
/// creation with a synthetic signature, and no RPC call is made. With it off the real path is not
/// implemented yet and fails loudly rather than silently reporting success — see the TODO in
/// <see cref="GetStatusAsync"/>.</para>
/// </summary>
public class SolanaPaymentProvider(
    IOptions<SolanaSettings> settings,
    TimeProvider timeProvider,
    ILogger<SolanaPaymentProvider> logger)
    : BaseService<SolanaPaymentProvider>(logger), IPaymentProvider
{
    private readonly SolanaSettings _settings = settings.Value;

    public PaymentProviderType Type => PaymentProviderType.Solana;

    /// <summary>
    /// True when confirmations are simulated. Read by the facade to gate the mock-confirm endpoint.
    /// </summary>
    public bool IsMock => _settings.UseMock;

    /// <summary>
    /// Currency code intents on this rail are denominated in.
    /// </summary>
    public const string Currency = "USDC";

    public PaymentInitiationResponse Create(PaymentIntent intent, Course course)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        intent.Provider = PaymentProviderType.Solana;
        intent.Currency = Currency;
        intent.Amount = course.PriceUsdc;
        intent.ReferenceKey = PaymentReferenceGenerator.NewReference();
        intent.CreatedAt = utcNow;
        intent.ExpiresAt = utcNow.AddMinutes(_settings.IntentTtlMinutes);

        Logger.LogInformation(
            "Created Solana payment intent {IntentId} for user {UserId} course {CourseId} amount {Amount} {Currency} (mock: {IsMock})",
            intent.Id, intent.UserId, intent.CourseId, intent.Amount, intent.Currency, IsMock);

        return Describe(intent, course);
    }

    public PaymentInitiationResponse Describe(PaymentIntent intent, Course course)
    {
        var payUrl = BuildPayUrl(intent, course);

        return new PaymentInitiationResponse(
            intent.Id,
            intent.Provider,
            intent.Amount,
            intent.Currency,
            payUrl,
            payUrl,
            intent.ReferenceKey,
            _settings.Cluster,
            intent.ExpiresAt,
            IsMock);
    }

    public Task<ProviderStatusResult> GetStatusAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        if (intent.Status != PaymentStatus.Pending)
        {
            return Task.FromResult(new ProviderStatusResult(intent.Status, intent.TransactionSignature));
        }

        if (!IsMock)
        {
            // TODO (Phase 3): verify on-chain with Solnet —
            //   getSignaturesForAddress(intent.ReferenceKey) → getTransaction(signature) →
            //   assert the SPL transfer moved >= intent.Amount of UsdcMint to TreasuryAddress.
            // Deliberately throwing instead of returning Pending: a misconfigured production
            // deployment must fail visibly, never quietly hold every payment at Pending.
            throw new SASException(ExceptionMessage.PaymentProviderNotAvailable, 503);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        if (ShouldMockConfirm(intent, utcNow))
        {
            return Task.FromResult(new ProviderStatusResult(PaymentStatus.Confirmed, SyntheticSignature(intent)));
        }

        return Task.FromResult(utcNow >= intent.ExpiresAt
            ? new ProviderStatusResult(PaymentStatus.Expired)
            : new ProviderStatusResult(PaymentStatus.Pending));
    }

    /// <summary>
    /// Signature handed out for a mocked confirmation. Derived from the intent id so repeated
    /// status checks of the same intent always yield the same value.
    /// </summary>
    public static string SyntheticSignature(PaymentIntent intent)
        => PaymentReferenceGenerator.ToBase58(SHA512.HashData(intent.Id.ToByteArray()));

    /// <summary>
    /// Mock confirmation is due once the configured delay has elapsed and the TTL has not.
    /// A negative <see cref="SolanaSettings.MockAutoConfirmSeconds"/> disables auto-confirmation,
    /// leaving the intent to expire — useful for exercising the expiry path.
    /// </summary>
    private bool ShouldMockConfirm(PaymentIntent intent, DateTime utcNow)
    {
        if (_settings.MockAutoConfirmSeconds < 0) return false;
        if (utcNow >= intent.ExpiresAt) return false;
        return utcNow >= intent.CreatedAt.AddSeconds(_settings.MockAutoConfirmSeconds);
    }

    /// <summary>
    /// Builds a Solana Pay transfer request URL.
    /// Spec: <c>solana:&lt;recipient&gt;?amount=&amp;spl-token=&amp;reference=&amp;label=&amp;message=</c>
    /// </summary>
    private string BuildPayUrl(PaymentIntent intent, Course course)
    {
        var recipient = string.IsNullOrWhiteSpace(_settings.TreasuryAddress)
            ? throw new SASException(ExceptionMessage.PaymentProviderNotAvailable, 503)
            : _settings.TreasuryAddress;

        var query = new List<string>
        {
            $"amount={intent.Amount.ToString("0.########", CultureInfo.InvariantCulture)}",
            $"reference={Uri.EscapeDataString(intent.ReferenceKey)}",
            $"label={Uri.EscapeDataString(_settings.Label)}",
            $"message={Uri.EscapeDataString(course.Title)}"
        };

        if (!string.IsNullOrWhiteSpace(_settings.UsdcMint))
        {
            query.Insert(1, $"spl-token={Uri.EscapeDataString(_settings.UsdcMint)}");
        }

        return $"solana:{recipient}?{string.Join('&', query)}";
    }
}
