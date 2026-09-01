using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses.Payments;

/// <summary>
/// Everything the client needs to present a payment: the deep link to open a wallet,
/// the same string to render as a QR, and the deadline to count down to.
/// </summary>
/// <param name="IntentId">Poll <c>GET /payments/intents/{id}/status</c> with this.</param>
/// <param name="Provider">Rail this intent runs on.</param>
/// <param name="Amount">Amount to be transferred.</param>
/// <param name="Currency">Currency of <paramref name="Amount"/> — "USDC" for Solana.</param>
/// <param name="PayUrl">Wallet deep link (<c>solana:...</c> for Solana Pay).</param>
/// <param name="QrPayload">Payload to encode as a QR. Identical to <paramref name="PayUrl"/> for Solana Pay.</param>
/// <param name="Reference">Solana Pay reference key identifying the transaction on-chain.</param>
/// <param name="Cluster">Network the wallet must use ("mainnet-beta" | "devnet").</param>
/// <param name="ExpiresAt">After this moment the intent is no longer payable.</param>
/// <param name="IsMock">True when confirmations are simulated — the client can show a test-mode badge.</param>
public record PaymentInitiationResponse(
    Guid IntentId,
    PaymentProviderType Provider,
    decimal Amount,
    string Currency,
    string PayUrl,
    string QrPayload,
    string Reference,
    string Cluster,
    DateTime ExpiresAt,
    bool IsMock);
