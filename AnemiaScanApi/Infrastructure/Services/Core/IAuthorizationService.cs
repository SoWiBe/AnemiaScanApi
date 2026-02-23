using AnemiaScanApi.Models.Auth;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IAuthorizationService
{
    public Task<TokenRecord> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    public Task<TokenRecord> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    public Task<TokenRecord> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
}