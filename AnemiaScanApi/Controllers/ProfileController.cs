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
        var consent = user.Consent is null
            ? null
            : new ConsentResponse(user.Consent.PolicyVersion, user.Consent.AcceptedAt, user.Consent.Accepted);

        return Ok(new GetProfileResponse(user.Email, user.FullName, user.BirthDate, user.Sex, user.Age,
            user.AnemiaScans, consent));
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

    /// <summary>
    /// Принять действующую версию политики конфиденциальности (P0 №13).
    /// Нужен, когда версия политики поднялась уже после регистрации: клиент
    /// видит расхождение версий и просит подписать заново.
    /// </summary>
    [HttpPost("consent/")]
    [ProducesResponseType(typeof(ConsentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptConsentAsync(AcceptConsentRequest request, CancellationToken cancellationToken = default)
    {
        var consent = await profileService.AcceptConsentAsync(GetUserId(), request.PolicyVersion, cancellationToken);
        return Ok(new ConsentResponse(consent.PolicyVersion, consent.AcceptedAt, consent.Accepted));
    }
}