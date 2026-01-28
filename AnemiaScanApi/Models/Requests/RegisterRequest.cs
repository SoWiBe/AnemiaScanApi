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
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [MaxLength(50, ErrorMessage = "Username must be at most 50 characters long")]
    public string Username { get; init; } = null!;
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