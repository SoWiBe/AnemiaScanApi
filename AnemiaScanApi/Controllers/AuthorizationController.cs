using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Models.Auth;
using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Models.Responses;
using AnemiaScanApi.Services.Core;

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
    /// <summary>
    /// Logs in a user.
    /// </summary>
    /// <param name="request">Login request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Login response.</returns>
    /// <remarks>
    ///     POST /api/authorization/login
    ///     Content-Type: application/json
    ///     
    ///     {
    ///         "username": "existinguser",
    ///         "password": "password123"
    ///     }
    /// </remarks>
    /// <response code="200">Login successful</response>
    /// <response code="400">Invalid username or password</response>
    /// <response code="500">Internal server error during login</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        return Ok(new LoginResponse { TokenRecord = tokenRecord });
    }
    
    /// <summary>
    /// Refreshes an access token.
    /// </summary>
    /// <param name="request">Refresh token request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Refresh token response.</returns>
    /// <remarks>
    ///     POST /api/authorization/refresh
    ///     Content-Type: application/json
    ///     
    ///     {
    ///         "refreshToken": "your_refresh_token_here"
    ///     }
    /// </remarks>
    /// <response code="200">Refresh successful</response>
    /// <response code="400">Invalid refresh token</response>
    /// <response code="500">Internal server error during refresh</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        return Ok(new RefreshTokenResponse { TokenRecord = tokenRecord });
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="request">Registration request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Registration response.</returns>
    /// <remarks>
    ///     POST /api/authorization/register
    ///     Content-Type: application/json
    ///     
    ///     {
    ///         "username": "newuser",
    ///         "password": "password123",
    ///         "confirmPassword": "password123"
    ///     }
    /// </remarks>
    /// <response code="200">Registration successful</response>
    /// <response code="400">Invalid username, password, or confirmation password</response>
    /// <response code="500">Internal server error during registration</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await authorizationService.RegisterAsync(request.Username, request.Password, cancellationToken);
        return Ok(new RegisterResponse { TokenRecord = tokenRecord });
    }
}