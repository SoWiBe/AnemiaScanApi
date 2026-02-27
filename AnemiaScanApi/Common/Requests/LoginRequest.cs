using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request for user login.
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = ValidationConstants.RequiredEmailErrorMessage)]
    [MaxLength(256, ErrorMessage = ValidationConstants.EmailShouldBeLessThan256CharactersErrorMessage)]
    [MinLength(1, ErrorMessage = ValidationConstants.RequiredEmailErrorMessage)]
    public string Email { get; init; } = null!;
    /// <summary>
    /// Password.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    public string Password { get; init; } = null!; 
}

public class LoginByCodeRequest : BaseAuthRequest
{
    /// <summary>
    /// Password.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    public string Password { get; init; } = null!; 
}