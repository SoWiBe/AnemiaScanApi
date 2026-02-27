using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests.Profile;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Infrastructure.Services;

public class ProfileService(
    ILogger<ProfileService> logger,
    IUsersRepository repository) 
    : BaseService<ProfileService>(logger), IProfileService
{
    public async Task<SasUser> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        => await repository.GetByIdAsync(userId, cancellationToken);

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

    private static Dictionary<string, Action<SasUser, string>> GetUpdateActions() => new()
    {
        ["Email"] = (user, value) => user.Email = value,
        ["FullName"] = (user, value) => user.FullName = value,
        ["BirthDate"] = (user, value) => user.BirthDate = DateTime.Parse(value),
        ["Password"] = (user, value) => user.HashPassword = BCrypt.Net.BCrypt.HashPassword(value),
    };
}