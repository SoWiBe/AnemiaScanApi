using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Запрос на обновление пароля.
/// </summary>
public class UpdatePasswordRequest
{
    /// <summary>
    /// Электронная почта.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredEmailErrorMessage)]
    public string Email { get; init; } = null!;

    /// <summary>
    /// Код из письма.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredEmailCodeErrorMessage)]
    public string Code { get; init; } = null!;

    /// <summary>
    /// Новый пароль.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    [MinLength(8, ErrorMessage = ValidationConstants.PasswordShouldBeAtLeast8CharactersErrorMessage)]
    public string NewPassword { get; init; } = null!;

    /// <summary>
    /// Подтверждение пароля.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.RequiredPasswordErrorMessage)]
    [Compare("NewPassword", ErrorMessage = ValidationConstants.PasswordsDoNotMatchErrorMessage)]
    public string ConfirmPassword { get; init; } = null!;
}
