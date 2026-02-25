using AnemiaScanApi.Common.Auth;

namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Response for refreshing an access token.
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>
    /// Token record.
    /// </summary>
    public TokenRecord TokenRecord { get; set; } = null!;
}