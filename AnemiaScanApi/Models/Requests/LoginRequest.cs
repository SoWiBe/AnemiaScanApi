namespace AnemiaScanApi.Models.Requests;

/// <summary>
/// Request for user login.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Username.
    /// </summary>
    public string Username { get; set; } = null!;
    /// <summary>
    /// Password.
    /// </summary>
    public string Password { get; set; } = null!;    
}