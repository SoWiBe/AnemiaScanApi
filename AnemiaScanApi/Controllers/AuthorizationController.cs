using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Common.Responses;
using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Filters;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.Utils;
using AnemiaScanApi.Utils.Core;
using Microsoft.Extensions.Caching.Memory;
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
    [HttpPost("sign-in")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ServiceFilter(typeof(ValidationCodeFilter))]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.AuthenticateAsync(request.Email!, request.Password, cancellationToken);
        return Ok(new LoginResponse { TokenRecord = tokenRecord });
    }
    
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        return Ok(new RefreshTokenResponse { TokenRecord = tokenRecord });
    }

    [HttpPost("sign-up")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ServiceFilter(typeof(ValidationCodeFilter))]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RegisterAsync(request.Email!, request.Password, cancellationToken);
        return Ok(new RegisterResponse { TokenRecord = tokenRecord });
    }

    [HttpPost("email/send-code")]
    public async Task<IActionResult> SendCodeAsync(SendCodeRequest request, CancellationToken cancellationToken = default)
    {
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
}