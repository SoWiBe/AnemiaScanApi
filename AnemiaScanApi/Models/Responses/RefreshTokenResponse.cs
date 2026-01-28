using AnemiaScanApi.Models.Auth;

namespace AnemiaScanApi.Models.Responses;

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