using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Models.Auth;
using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Models.Responses;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for authorization-related operations.
/// </summary>
[ApiController]
[Route("[controller]")]
public class AuthorizationController(
    ILogger<AuthorizationController> logger, 
    IAuthorizationService authorizationService) : BaseSasController(logger)
{
    [HttpPost("sign-in")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.AuthenticateAsync(request.Email, request.Password, cancellationToken);
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
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RegisterAsync(request.Username, request.Password, cancellationToken);
        return Ok(new RegisterResponse { TokenRecord = tokenRecord });
    }
}