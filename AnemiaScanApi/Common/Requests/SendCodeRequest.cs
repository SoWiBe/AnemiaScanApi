using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests;

public class SendCodeRequest
{
    [Required(ErrorMessage = "Пожалуйста, укажите вашу почту")]
    public string? Email { get; set; }
}