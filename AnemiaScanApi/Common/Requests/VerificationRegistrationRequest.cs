using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request model for verifying registration data before sending a code.
/// </summary>
public class VerificationRegistrationRequest
{
    /// <summary>
    /// Email address.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredEmailErrorMessage)]
    [EmailAddress(ErrorMessage = "Неверный адрес электронной почты")]
    public string Email { get; init; } = null!;

    /// <summary>
    /// Birth date.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredBirthDateErrorMessage)]
    public DateTime? BirthDate { get; init; }

    /// <summary>
    /// Password.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    [MinLength(8, ErrorMessage = ValidationConstants.InvalidPasswordErrorMessage)]
    public string Password { get; init; } = null!;

    /// <summary>
    /// Confirm password.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    [Compare("Password", ErrorMessage = ValidationConstants.PasswordsDoNotMatchErrorMessage)]
    public string ConfirmPassword { get; init; } = null!;
}
