using MongoDB.Bson;

namespace AnemiaScanApi.Models.Responses;

/// <summary>
/// Response model for creating a new user.
/// </summary>
public class CreateUserResponse
{
    /// <summary>
    /// The Guid of the created user.
    /// </summary>
    public Guid UserId { get; set; }
}