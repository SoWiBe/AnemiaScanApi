using AnemiaScanApi.Common.Auth;

namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Response for registering a new user.
/// </summary>
public class RegisterResponse
{
    /// <summary>
    /// Token record.
    /// </summary>
    public TokenRecord TokenRecord { get; init; } = null!;    
}