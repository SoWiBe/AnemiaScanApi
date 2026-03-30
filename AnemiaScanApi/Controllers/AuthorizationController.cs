using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Common.Responses;
using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Filters;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils.Core;
using Microsoft.Extensions.Caching.Memory;

using Microsoft.AspNetCore.Authorization;
using IAuthorizationService = AnemiaScanApi.Infrastructure.Services.Core.IAuthorizationService;
using IEmailSender = AnemiaScanApi.Utils.Core.IEmailSender;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for authorization-related operations.
/// </summary>
[ApiController]
[Route("[controller]")]
public class AuthorizationController(
    ILogger<AuthorizationController> logger, 
    IAuthorizationService authorizationService,
    IEmailSender emailSender,
    ICodeGenerator codeGenerator,
    IMemoryCache memoryCache) : BaseSasController(logger)
{
    /// <summary>
    /// Авторизация по логину и паролю.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("sign-in/")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.AuthenticateAsync(request.Email!, request.Password, cancellationToken);
        return Ok(new LoginResponse { TokenRecord = tokenRecord });
    }
    
    /// <summary>
    /// Авторизация по логину и паролю с подтверждением по почте.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("sign-in-by-code/")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ServiceFilter(typeof(ValidationCodeFilter))]
    public async Task<IActionResult> LoginByCodeAsync(LoginByCodeRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.AuthenticateAsync(request.Email!, request.Password, cancellationToken);
        return Ok(new LoginResponse { TokenRecord = tokenRecord });
    }
    
    /// <summary>
    /// Обновление токена.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("refresh/")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        return Ok(new RefreshTokenResponse { TokenRecord = tokenRecord });
    }

    /// <summary>
    /// Регистрация нового пользователя.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("sign-up/")]
    [ProducesResponseType(typeof(SignUpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ServiceFilter(typeof(ValidationCodeFilter))]
    public async Task<IActionResult> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RegisterAsync(request, cancellationToken);
        return Ok(new SignUpResponse { TokenRecord = tokenRecord });
    }

    /// <summary>
    /// Отправка кода подтверждения на почту.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("email/send-code/")]
    public async Task<IActionResult> SendCodeAsync(SendCodeRequest request, CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.IsUserExistAsync(request.Email!, cancellationToken))
            return NotFound(new { message = "пользователь не найден в системе, пройдите этап регистрации для этого" });
        
        var (cachingKey, code) = codeGenerator.GenerateAlphanumericCode(request.Email!);
        await emailSender.SendEmailAsync(request.Email!, "Smart Anemia Scan Verification Code",
            $"Your verification code is: {code}", cancellationToken);
        
        // кэшируем код на 5 минут
        memoryCache.Set(cachingKey, code, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(5),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });
        
        return Ok();
    }

    /// <summary>
    /// Проверка кода верификации.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("verification-code/")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyCodeAsync(VerificationCodeRequest request, CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.IsUserExistAsync(request.Email, cancellationToken))
            return NotFound(new { message = "пользователь не найден в системе, пройдите этап регистрации для этого" });

        var cachingKey = codeGenerator.GetCacheKey(request.Email);
        var cachedCode = memoryCache.Get<string>(cachingKey);

        if (string.IsNullOrWhiteSpace(cachedCode) || !cachedCode.Equals(request.Code))
            return BadRequest(new { message = "Указан неправильный код подтверждения или код истек" });

        return Ok();
    }

    /// <summary>
    /// Обновление пароля (забытый пароль).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("update-password/")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePasswordAsync(UpdatePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!await authorizationService.IsUserExistAsync(request.Email, cancellationToken))
            return NotFound(new { message = "пользователь не найден в системе, пройдите этап регистрации для этого" });

        var cachingKey = codeGenerator.GetCacheKey(request.Email);
        var cachedCode = memoryCache.Get<string>(cachingKey);

        if (string.IsNullOrWhiteSpace(cachedCode) || !cachedCode.Equals(request.Code))
            return BadRequest(new { message = "Указан неправильный код подтверждения или код истек" });

        await authorizationService.UpdatePasswordAsync(request.Email, request.NewPassword, cancellationToken);
        
        // удаляем код после успешного обновления
        memoryCache.Remove(cachingKey);

        return Ok();
    }
}