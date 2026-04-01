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
    /// Full name.
    /// </summary>
    [BsonElement("full_name")] public string FullName { get; set; } = null!;
    /// <summary>
    /// Date of birth.
    /// </summary>
    [BsonElement("birth_date")] public DateTime? BirthDate { get; set; }
    /// <summary>
    /// Sex.
    /// </summary>
    [BsonElement("sex")] public Enums.Sex? Sex { get; set; }
    /// <summary>
    /// Gender.
    /// </summary>
    [BsonElement("gender")] public Enums.Gender? Gender { get; set; }
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
    /// <summary>
    /// Date and time when the user was created.
    /// </summary>
    [BsonElement("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Date and time when the user was last updated.
    /// </summary>
    [BsonElement("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Anemia scans.
    /// </summary>
    [BsonElement("anemia_scans")] public List<AnemiaScan> AnemiaScans { get; set; } = new();
}