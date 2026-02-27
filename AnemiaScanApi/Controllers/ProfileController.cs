using AnemiaScanApi.Common.Requests.Profile;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Infrastructure.Services.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class ProfileController(
    ILogger<ProfileController> logger,
    IProfileService profileService) : BaseSasController(logger)
{
    [HttpGet("info/")]
    [ProducesResponseType(typeof(GetProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var user = await profileService.GetProfileAsync(GetUserId(), cancellationToken);
        return Ok(new GetProfileResponse { Profile = user });
    }
    
    [HttpPatch]
    [ProducesResponseType(typeof(UpdateProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        await profileService.UpdateProfileAsync(GetUserId(), request, cancellationToken);
        return Ok(new UpdateProfileResponse { Message = "Данные о пользователе успешно обновлены" });
    }
}