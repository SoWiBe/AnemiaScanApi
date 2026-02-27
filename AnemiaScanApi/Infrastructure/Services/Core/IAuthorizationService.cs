using AnemiaScanApi.Common.Auth;
using AnemiaScanApi.Common.Requests;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IAuthorizationService
{
    public Task<TokenRecord> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    public Task<TokenRecord> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    public Task<TokenRecord> RegisterAsync(SignUpRequest request, CancellationToken cancellationToken = default);
}