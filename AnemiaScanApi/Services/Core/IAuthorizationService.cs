using AnemiaScanApi.Models.Auth;

namespace AnemiaScanApi.Services.Core;

/// <summary>
/// Service for authorization-related operations.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Authenticates a user.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <param name="password">The password of the user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The JWT token for the authenticated user.</returns>
    public Task<TokenRecord> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes a JWT token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The refreshed JWT token.</returns>
    public Task<TokenRecord> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="username">The username of the new user.</param>
    /// <param name="password">The password of the new user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The JWT token for the registered user.</returns>
    public Task<TokenRecord> RegisterAsync(string username, string password, CancellationToken cancellationToken = default);
}