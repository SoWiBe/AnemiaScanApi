using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Models;

/// <summary>
/// Model for a user in the system.
/// </summary>
public class SasUser : BaseMongoModel
{
    /// <summary>
    /// Username.
    /// </summary>
    [BsonElement("username")] public string Username { get; set; } = null!;
    /// <summary>
    /// Hashed password. 
    /// </summary>
    [BsonElement("hash_password")] public string HashPassword { get; set; } = null!;
    /// <summary>
    /// Refresh token.
    /// </summary>
    [BsonElement("refresh_token")] public string? RefreshToken { get; set; }
    /// <summary>
    /// Expiration time for refresh token.
    /// </summary>
    [BsonElement("refresh_token_expires")] public DateTime? RefreshTokenExpires { get; set; }
}