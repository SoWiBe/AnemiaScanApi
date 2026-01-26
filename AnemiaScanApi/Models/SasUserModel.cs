using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Models;

/// <summary>
/// Model for a user in the system.
/// </summary>
public class SasUserModel : BaseMongoModel
{
    /// <summary>
    /// Username.
    /// </summary>
    [BsonElement("username")] public string Username { get; set; } = null!;
    /// <summary>
    /// Hashed password. 
    /// </summary>
    [BsonElement("hash_password")] public string HashPassword { get; set; } = null!;
}