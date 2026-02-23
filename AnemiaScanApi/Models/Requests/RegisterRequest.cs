using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Models.Requests;

/// <summary>
/// Request for registering a new user.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Username.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    public string Email { get; init; } = null!;
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