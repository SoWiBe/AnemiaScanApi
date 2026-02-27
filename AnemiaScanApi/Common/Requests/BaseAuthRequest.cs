using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests;

public class BaseAuthRequest
{
    [Required(ErrorMessage = ValidationConstants.RequiredEmailErrorMessage)]
    [MaxLength(256, ErrorMessage = ValidationConstants.EmailShouldBeLessThan256CharactersErrorMessage)]
    [MinLength(1, ErrorMessage = ValidationConstants.RequiredEmailErrorMessage)]
    public string? Email { get; init; }
    
    [Required(ErrorMessage = ValidationConstants.RequiredEmailCodeErrorMessage)]
    public string? EmailCode { get; init; }
}