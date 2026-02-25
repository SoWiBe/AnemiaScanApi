using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class UsersRepository(IOptions<MongoDbSettings> mongoDbSettings, ILogger<UsersRepository> logger)
    : BaseMongoRepository<SasUser>(mongoDbSettings, MongoCollection.Users, logger), IUsersRepository
{
    public Task<SasUser> CreateUserAsync(SasUser user, CancellationToken cancellationToken = default)
         => Collection
             .InsertOneAsync(user, cancellationToken: cancellationToken)
             .ContinueWith(_ => user, cancellationToken);
    
    public Task<SasUser> UpdateUserAsync(SasUser user, CancellationToken cancellationToken = default)
        => Collection
            .ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken)
            .ContinueWith(_ => user, cancellationToken);

    public Task<bool> IsEmailUnique(string email, CancellationToken cancellationToken = default)
        => Collection.CountDocumentsAsync(x => x.Email == email, cancellationToken: cancellationToken)
            .ContinueWith(count => count.Result == 0, cancellationToken);

    public async Task<SasUser> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.Email == email)
            .FirstOrDefaultAsync(cancellationToken);
}