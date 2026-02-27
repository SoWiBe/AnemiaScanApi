using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Request for registering a new user.
/// </summary>
public class SignUpRequest : BaseAuthRequest
{
    /// <summary>
    /// Полное имя пользователя.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredFullNameErrorMessage)]
    [StringLength(256, MinimumLength = 2, ErrorMessage = ValidationConstants.InvalidFullNameErrorMessage)]
    public string FullName { get; init; } = null!;
    /// <summary>
    /// Дата рождения.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredBirthDateErrorMessage)]
    public DateTime? BirthDate { get; init; }
    /// <summary>
    /// Пароль.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    [MinLength(8, ErrorMessage = ValidationConstants.InvalidPasswordErrorMessage)]
    public string Password { get; init; } = null!;
    /// <summary>
    /// Подтверждение пароля.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    [Compare("Password", ErrorMessage = ValidationConstants.PasswordsDoNotMatchErrorMessage)]
    public string ConfirmPassword { get; init; } = null!;
}