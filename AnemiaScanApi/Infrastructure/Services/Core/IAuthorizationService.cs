using AnemiaScanApi.Common.Auth;
using AnemiaScanApi.Common.Requests;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IAuthorizationService
{
    public Task<TokenRecord> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    public Task<TokenRecord> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    public Task<TokenRecord> RegisterAsync(SignUpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет существование пользователя по email.
    /// </summary>
    public Task<bool> IsUserExistAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет пароль пользователя.
    /// </summary>
    public Task UpdatePasswordAsync(string email, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the registration request (email, birthdate, password) is valid.
    /// Throws <see cref="InvalidOperationException"/> if verification fails.
    /// </summary>
    public Task VerifyRegistrationRequestAsync(VerificationRegistrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the refresh token for the authenticated user, effectively signing them out.
    /// </summary>
    public Task SignOutAsync(Guid userId, CancellationToken cancellationToken = default);
}