namespace AnemiaScanApi.Common.Enums;

/// <summary>
/// Payment rails supported by the payment abstraction.
/// </summary>
public enum PaymentProviderType
{
    /// <summary>
    /// Solana Pay — USDC transfer to the project treasury.
    /// </summary>
    Solana = 0,

    /// <summary>
    /// Kaspi/card. Stub until Phase 3.
    /// </summary>
    Kaspi = 1
}
