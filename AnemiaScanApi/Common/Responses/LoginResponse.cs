using AnemiaScanApi.Common.Auth;

namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Response for user login.
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT token.
    /// </summary>
    public TokenRecord TokenRecord { get; set; }    
}