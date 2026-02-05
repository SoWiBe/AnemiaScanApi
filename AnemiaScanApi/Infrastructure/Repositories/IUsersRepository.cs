using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Models;
using AnemiaScanApi.Services.Core;

namespace AnemiaScanApi.Infrastructure.Repositories;

/// <summary>
/// Interface for user-related MongoDB service operations.
/// </summary>
public interface IUsersRepository : IMongoRepository<SasUser>
{
    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The user with the specified username.</returns>
    public Task<SasUser> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created user.</returns>
    public Task<SasUser> CreateUserAsync(SasUser user, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated user.</returns>
    public Task<SasUser> UpdateUserAsync(SasUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a username is unique.
    /// </summary>
    /// <param name="username">The username to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the username is unique, false otherwise.</returns>
    public Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default);
}