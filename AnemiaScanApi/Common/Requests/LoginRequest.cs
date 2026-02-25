namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request for user login.
/// </summary>
public class LoginRequest : BaseAuthRequest
{
    /// <summary>
    /// Password.
    /// </summary>
    public string Password { get; init; } = null!; 
}