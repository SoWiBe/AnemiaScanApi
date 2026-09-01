namespace AnemiaScanApi.Common.Enums;

/// <summary>
/// Lifecycle of a <see cref="PaymentIntent"/>.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Created, waiting for an on-chain (or provider-side) confirmation.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Payment observed and settled. Terminal.
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// TTL elapsed without a confirmation. Terminal.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// Provider reported a hard failure. Terminal.
    /// </summary>
    Failed = 3
}
