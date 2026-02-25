using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Model for a user in the system.
/// </summary>
public class SasUser : BaseMongoModel
{
    /// <summary>
    /// Email.
    /// </summary>
    /// <value></value>
    [BsonElement("email")] public string Email { get; set; } = null!;
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