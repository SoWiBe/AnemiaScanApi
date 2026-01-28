using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AnemiaScanApi.Models;
using AnemiaScanApi.Models.Auth;
using AnemiaScanApi.Services.Core;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AnemiaScanApi.Services;

/// <summary>
/// Service for authorization-related operations.
/// </summary>
public class AuthorizationService(
    ILogger<AuthorizationService> logger, 
    IUserService userService,
    IOptions<JwtSettings> jwtSettings) 
    : BaseService<AuthorizationService>(logger), IAuthorizationService
{
    /// <summary>
    /// Authenticates a user.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <param name="password">The password of the user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The JWT token for the authenticated user.</returns>
    public async Task<TokenRecord> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Authenticating user {Username}", username);
        
        var user = await userService.GetUserByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            Logger.LogError("User {Username} not found", username);
            throw new UnauthorizedAccessException("Invalid username or password");
        }
        
        if (!BCrypt.Net.BCrypt.Verify(password, user.HashPassword))
        {
            Logger.LogError("Invalid password for user {Username}", username);
            throw new UnauthorizedAccessException("Invalid username or password");
        }
        
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        Logger.LogInformation("Generated access token for user {Username}", username);
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpires = DateTime.UtcNow.AddMinutes(jwtSettings.Value.RefreshTokenExpirationDays);
        await userService.UpdateUserAsync(user, cancellationToken);
        
        Logger.LogInformation("TokenRecord generated for user {Username}", username);

        return new TokenRecord(accessToken, refreshToken);
    }

    /// <summary>
    /// Refreshes a JWT token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The refreshed JWT token.</returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    public async Task<TokenRecord> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Refreshing token");
        
        var users = await userService.GetAllAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.RefreshToken == refreshToken);

        if (user == null || user.RefreshTokenExpires < DateTime.UtcNow)
        {
            Logger.LogWarning("Token refresh failed: Invalid or expired refresh token");
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        // Generate new tokens
        var accessToken = GenerateAccessToken(user);
        var newRefreshToken = GenerateRefreshToken();

        // Update refresh token in database
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpires = DateTime.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        await userService.UpdateAsync(user.Id!, user, cancellationToken);

        Logger.LogInformation("Token refreshed successfully for user: {Username}", user.Username);
        return new TokenRecord(accessToken, newRefreshToken);
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="password">The plain text password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token containing access and refresh tokens</returns>
    public async Task<TokenRecord> RegisterAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Registering new user: {Username}", username);
        
        // Check if user already exists
        var users = await userService.GetAllAsync(cancellationToken);
        if (users.Any(u => u.Username == username))
        {
            Logger.LogWarning("Registration failed: Username already exists - {Username}", username);
            throw new InvalidOperationException("Username already exists");
        }

        // Create new user
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var newUser = new SasUser
        {
            Username = username,
            HashPassword = hashedPassword
        };

        var createdUser = await userService.CreateUserAsync(newUser, cancellationToken);

        // Generate tokens
        var accessToken = GenerateAccessToken(createdUser);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token
        createdUser.RefreshToken = refreshToken;
        createdUser.RefreshTokenExpires = DateTime.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        await userService.UpdateUserAsync(createdUser, cancellationToken);

        Logger.LogInformation("User registered successfully: {Username}", username);
        return new TokenRecord(accessToken, refreshToken);
    }
    
    /// <summary>
    /// Generates a JWT access token for a user.
    /// </summary>
    private string GenerateAccessToken(SasUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtSettings.Value.Secret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings.Value.AccessTokenExpirationMinutes),
            Issuer = jwtSettings.Value.Issuer,
            Audience = jwtSettings.Value.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}