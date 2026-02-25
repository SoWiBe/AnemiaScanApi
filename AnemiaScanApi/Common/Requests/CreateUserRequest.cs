using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request model for creating a new user.
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Email
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Password.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string Password { get; set; } = null!;
    
    /// <summary>
    /// Repeat Password.
    /// </summary>
    [Required(ErrorMessage = "Repeat Password is required")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string RepeatPassword { get; set; } = null!;
}