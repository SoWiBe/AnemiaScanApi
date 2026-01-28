using AnemiaScanApi.Models;
using AnemiaScanApi.Services.Core;
using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AnemiaScanApi.Services;

/// <summary>
/// Service for user-related MongoDB operations.
/// </summary>
/// <param name="mongoDbSettings"></param>
public class UserService(IOptions<MongoDbSettings> mongoDbSettings)
    : BaseMongoService<SasUser>(mongoDbSettings, "users"), IUserService
{
    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The user with the specified username.</returns>
    public async Task<SasUser> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Username == username)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created user.</returns>
    public Task<SasUser> CreateUserAsync(SasUser user, CancellationToken cancellationToken = default)
         => Collection
             .InsertOneAsync(user, cancellationToken: cancellationToken)
             .ContinueWith(_ => user, cancellationToken);
    
    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated user.</returns>
    public Task<SasUser> UpdateUserAsync(SasUser user, CancellationToken cancellationToken = default)
        => Collection
            .ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken)
            .ContinueWith(_ => user, cancellationToken);

    /// <summary>
    /// Checks if a username is unique.
    /// </summary>
    /// <param name="username">The username to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the username is unique, false otherwise.</returns>
    public Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default)
        => Collection.CountDocumentsAsync(x => x.Username == username, cancellationToken: cancellationToken)
            .ContinueWith(count => count.Result == 0, cancellationToken);
}