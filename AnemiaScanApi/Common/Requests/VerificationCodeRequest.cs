using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests;

/// <summary>
/// Запрос на подтверждение кода из почты.
/// </summary>
public class VerificationCodeRequest
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
}
