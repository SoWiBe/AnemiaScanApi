using AnemiaScanApi.Common;
using AnemiaScanApi.Infrastructure.Core;

namespace AnemiaScanApi.Infrastructure.Repositories;

public interface IUsersRepository : IMongoRepository<SasUser>
{
    public Task<SasUser> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    public Task<SasUser> CreateUserAsync(SasUser user, CancellationToken cancellationToken = default);
    public Task<SasUser> UpdateUserAsync(SasUser user, CancellationToken cancellationToken = default);
    public Task<bool> IsEmailUnique(string email, CancellationToken cancellationToken = default);
}