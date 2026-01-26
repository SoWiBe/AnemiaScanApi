using AnemiaScanApi.Models;
using AnemiaScanApi.Services.Core;

namespace AnemiaScanApi.Services;

/// <summary>
/// Interface for user-related MongoDB service operations.
/// </summary>
public interface IUserService : IMongoService<SasUserModel>
{
    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The user with the specified username.</returns>
    public Task<SasUserModel> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created user.</returns>
    public Task<SasUserModel> CreateUserAsync(SasUserModel user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a username is unique.
    /// </summary>
    /// <param name="username">The username to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the username is unique, false otherwise.</returns>
    public Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default);
}