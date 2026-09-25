using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests.Profile;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Infrastructure.Services;

public class ProfileService(
    ILogger<ProfileService> logger,
    IUsersRepository repository,
    IOptions<LegalSettings> legalSettings) 
    : BaseService<ProfileService>(logger), IProfileService
{
    public async Task<SasUser> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken);
        // Раньше null уходил в контроллер и отдавался как 200 с пустым профилем; теперь
        // контроллер читает поля пользователя напрямую, так что null здесь — это 500.
        if (user is null) throw new SASException(ExceptionMessage.ProfileNotFound, 404);

        return user;
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new SASException("Пользователь не найден в системе");
        
        var updateActions = GetUpdateActions();
        if (request.Email is not null) updateActions["Email"](user, request.Email);
        if (request.FullName is not null) updateActions["FullName"](user, request.FullName);
        if (request.BirthDate is not null) updateActions["BirthDate"](user, request.BirthDate.Value.ToString());
        if (request.Password is not null)
        {
            if (request.ConfirmPassword is null || request.Password != request.ConfirmPassword)
                throw new SASException("Пароли не совпадают");
            
            updateActions["Password"](user, request.Password);
        }

        if (request.Sex is not null) user.Sex = request.Sex;
        if (request.Age is not null) user.Age = request.Age;
        
        user.UpdatedAt = DateTime.UtcNow;
        _ = await repository.UpdateAsync(userId, user, cancellationToken);
    }

    public async Task WriteAnalysisAsync(Guid userId, AnemiaScan scan, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new SASException(ExceptionMessage.ProfileNotFound);
        
        user.AnemiaScans.Add(scan);
        await repository.UpdateAsync(userId, user, cancellationToken);
    }

    public async Task DeleteProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new SASException(ExceptionMessage.ProfileNotFound);
        
        await repository.DeleteAsync(userId, cancellationToken);
    }

    public async Task<UserConsent> AcceptConsentAsync(Guid userId, string? policyVersion, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new SASException(ExceptionMessage.ProfileNotFound, 404);

        var currentVersion = legalSettings.Value.PolicyVersion;
        if (!string.IsNullOrWhiteSpace(policyVersion) && policyVersion != currentVersion)
            throw new SASException(ExceptionMessage.ConsentVersionMismatch, 400);

        user.Consent = new UserConsent
        {
            PolicyVersion = currentVersion,
            AcceptedAt = DateTime.UtcNow,
            Accepted = true
        };
        user.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(userId, user, cancellationToken);
        Logger.LogInformation("Consent for policy {PolicyVersion} recorded for user {UserId}", currentVersion, userId);

        return user.Consent;
    }

    private static Dictionary<string, Action<SasUser, string>> GetUpdateActions() => new()
    {
        ["Email"] = (user, value) => user.Email = value,
        ["FullName"] = (user, value) => user.FullName = value,
        ["BirthDate"] = (user, value) => user.BirthDate = DateTime.Parse(value),
        ["Password"] = (user, value) => user.HashPassword = BCrypt.Net.BCrypt.HashPassword(value),
    };
}