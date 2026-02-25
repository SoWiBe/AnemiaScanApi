namespace AnemiaScanApi.Settings;

/// <summary>
/// Настройки для SMTP
/// </summary>
public class SmtpSettings
{
    /// <summary>
    /// Server
    /// </summary>
    public string Server { get; set; } = null!;
    /// <summary>
    /// Port
    /// </summary>
    public int Port { get; set; }
}