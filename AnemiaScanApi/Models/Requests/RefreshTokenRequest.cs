using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Models.Requests;

/// <summary>
/// Request for refreshing an access token.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token.
    /// </summary>
    [Required] 
    public string RefreshToken { get; init; } = null!;
}