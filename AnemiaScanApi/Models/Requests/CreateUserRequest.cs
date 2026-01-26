using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Models.Requests;

/// <summary>
/// Request model for creating a new user.
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Username.
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(50, ErrorMessage = "Username must be at most 50 characters long")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    
    public string Username { get; set; } = null!;

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