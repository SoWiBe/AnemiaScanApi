using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Controllers.Core;
using AnemiaScanApi.Models.Auth;
using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Models.Responses;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for authorization-related operations.
/// </summary>
[ApiController]
[Route("[controller]")]
public class AuthorizationController(ILogger<AuthorizationController> logger) : BaseSasController(logger)
{
    /// <summary>
    /// Logs in a user.
    /// </summary>
    /// <param name="request">Login request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Login response.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: Perform authentication and return token
        return Ok(new LoginResponse { TokenRecord = new TokenRecord("access_token", "refresh_token") });
    }    
}