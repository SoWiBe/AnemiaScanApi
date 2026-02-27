using System.ComponentModel.DataAnnotations;
using AnemiaScanApi.Common.Constants;

namespace AnemiaScanApi.Common.Requests.Profile;

public class UpdateProfileRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public DateTime? BirthDate { get; set; }
    [MinLength(8, ErrorMessage = ValidationConstants.PasswordShouldBeAtLeast8CharactersErrorMessage)]
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
}