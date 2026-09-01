namespace AnemiaScanApi.Settings;

/// <summary>
/// Solana Pay configuration. Real values live in User Secrets / environment —
/// <c>appsettings.json</c> ships with blanks like the other secret sections.
/// </summary>
public class SolanaSettings
{
    /// <summary>
    /// RPC endpoint used to verify transactions. Unused while <see cref="UseMock"/> is true.
    /// </summary>
    public string? RpcUrl { get; set; }

    /// <summary>
    /// Cluster label ("mainnet-beta" | "devnet"), surfaced to the client so the wallet
    /// connects to the same network we watch.
    /// </summary>
    public string Cluster { get; set; } = "devnet";

    /// <summary>
    /// Project treasury address that receives the USDC transfer.
    /// </summary>
    public string? TreasuryAddress { get; set; }

    /// <summary>
    /// SPL mint of the token being charged (USDC).
    /// </summary>
    public string? UsdcMint { get; set; }

    /// <summary>
    /// When true, confirmations are simulated and no RPC call is made.
    /// Must be false in production — the real provider path refuses to run otherwise.
    /// </summary>
    public bool UseMock { get; set; } = true;

    /// <summary>
    /// Mock only: seconds after creation at which an intent reports itself confirmed.
    /// Zero means "confirmed on the very first status check".
    /// </summary>
    public int MockAutoConfirmSeconds { get; set; } = 10;

    /// <summary>
    /// How long an intent stays payable before it expires.
    /// </summary>
    public int IntentTtlMinutes { get; set; } = 15;

    /// <summary>
    /// Label shown by the wallet on the payment request.
    /// </summary>
    public string Label { get; set; } = "AnemiaScan";
}
