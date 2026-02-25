using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request for registering a new user.
/// </summary>
public class RegisterRequest : BaseAuthRequest
{
    /// <summary>
    /// Password.
    /// </summary>
    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string Password { get; init; } = null!;
    /// <summary>
    /// Confirm password.
    /// </summary>
    [Required]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; init; } = null!;
}