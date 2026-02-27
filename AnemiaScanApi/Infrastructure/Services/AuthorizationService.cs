using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Auth;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Settings;

namespace AnemiaScanApi.Infrastructure.Services;

public class AuthorizationService(
    ILogger<AuthorizationService> logger, 
    IUsersRepository usersRepository,
    IOptions<JwtSettings> jwtSettings) 
    : BaseService<AuthorizationService>(logger), IAuthorizationService
{
    public async Task<TokenRecord> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Authenticating user {Email}", email);
        
        var user = await usersRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            Logger.LogError("User {Email} not found", email);
            throw new UnauthorizedAccessException(ExceptionMessage.InvalidEmailOrPassword);
        }
        
        if (!BCrypt.Net.BCrypt.Verify(password, user.HashPassword))
        {
            Logger.LogError("Invalid password for user {Email}", email);
            throw new UnauthorizedAccessException(ExceptionMessage.InvalidEmailOrPassword);
        }
        
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        Logger.LogInformation("Generated access token for user {Email}", email);
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpires = DateTime.UtcNow.AddMinutes(jwtSettings.Value.RefreshTokenExpirationDays);
        await usersRepository.UpdateUserAsync(user, cancellationToken);
        
        Logger.LogInformation("TokenRecord generated for user {Email}", email);

        return new TokenRecord(accessToken, refreshToken);
    }

    public async Task<TokenRecord> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Refreshing token");
        
        var users = await usersRepository.GetAllAsync(cancellationToken);
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
        await usersRepository.UpdateAsync(user.Id!, user, cancellationToken);

        Logger.LogInformation("Token refreshed successfully for user: {Email}", user.Email);
        return new TokenRecord(accessToken, newRefreshToken);
    }

    public async Task<TokenRecord> RegisterAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Registering new user: {Email}", request.Email);
        
        // Check if user already exists
        var users = await usersRepository.GetAllAsync(cancellationToken);
        if (users.Any(u => u.Email == request.Email))
        {
            Logger.LogWarning("Registration failed: Email already exists - {Email}", request.Email);
            throw new InvalidOperationException("Email already exists");
        }

        // Create new user
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var newUser = new SasUser
        {
            Email = request.Email,
            FullName = request.FullName,
            BirthDate = request.BirthDate,
            HashPassword = hashedPassword,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var createdUser = await usersRepository.CreateUserAsync(newUser, cancellationToken);

        // Generate tokens
        var accessToken = GenerateAccessToken(createdUser);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token
        createdUser.RefreshToken = refreshToken;
        createdUser.RefreshTokenExpires = DateTime.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        await usersRepository.UpdateUserAsync(createdUser, cancellationToken);

        Logger.LogInformation("User registered successfully: {Email}", createdUser.Email);
        return new TokenRecord(accessToken, refreshToken);
    }
    
    private string GenerateAccessToken(SasUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtSettings.Value.Secret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
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