namespace AnemiaScanApi.Settings;

/// <summary>
/// Настройки для Email sender
/// </summary>
public class EmailSenderSettings
{
    /// <summary>
    /// Email
    /// </summary>
    public string Email { get; set; } = null!;
    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = null!;
}