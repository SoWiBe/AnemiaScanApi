using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Settings;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Юридические тексты: версия политики конфиденциальности и медицинский
/// дисклеймер (P0 №12 и №13 в docs/plans/MVP_PLAN.md).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("[controller]")]
public class LegalController(
    ILogger<LegalController> logger,
    IOptions<LegalSettings> legalSettings) : BaseSasController(logger)
{
    /// <summary>
    /// Действующая политика и дисклеймер. Фронт берёт текст отсюда, а не
    /// хардкодит: правки формулировок не должны требовать релиза мобилки.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(LegalResponse), StatusCodes.Status200OK)]
    public IActionResult GetLegal()
    {
        var settings = legalSettings.Value;

        return Ok(new LegalResponse(
            settings.PolicyVersion,
            settings.PolicyUrl,
            MedicalDisclaimer.Version,
            string.IsNullOrWhiteSpace(settings.DisclaimerText) ? MedicalDisclaimer.Text : settings.DisclaimerText));
    }
}
