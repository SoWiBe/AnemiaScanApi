using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests;

public class BaseAuthRequest
{
    [Required(ErrorMessage = "Пожалуйста, укажите вашу почту")]
    public string? Email { get; init; }
    
    [Required(ErrorMessage = "Пожалуйста, укажите код из письма на вашей почте")] 
    public string? EmailCode { get; init; }
}