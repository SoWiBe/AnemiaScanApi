namespace AnemiaScanApi.Settings;

/// <summary>
/// JWT configuration settings.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Secret key for JWT signing.
    /// </summary>
    public string Secret { get; set; } = null!;
    /// <summary>
    /// Issuer of the JWT.
    /// </summary>
    public string Issuer { get; set; } = null!;
    /// <summary>
    /// Audience for the JWT.
    /// </summary>
    public string Audience { get; set; } = null!;
    /// <summary>
    /// Expiration time for access tokens in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; }
    /// <summary>
    /// Expiration time for refresh tokens in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; }
}