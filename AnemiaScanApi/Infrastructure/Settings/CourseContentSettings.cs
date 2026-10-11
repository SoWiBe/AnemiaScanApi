namespace AnemiaScanApi.Settings;

/// <summary>
/// Откуда и когда загружать курсы (секция <c>CourseContent</c>).
/// </summary>
public class CourseContentSettings
{
    /// <summary>Папка по умолчанию — едет в публикацию вместе с приложением.</summary>
    public const string DefaultPath = "Content/Courses";

    /// <summary>
    /// Папка с файлами курсов. Относительный путь считается от каталога
    /// приложения, а не от текущего каталога процесса: под systemd это разные вещи.
    /// </summary>
    public string Path { get; set; } = DefaultPath;

    /// <summary>
    /// Синхронизировать папку с базой при старте сервиса. Выключается, если
    /// курсы нужно временно заморозить в базе независимо от того, что лежит в
    /// репозитории.
    /// </summary>
    public bool ImportOnStartup { get; set; } = true;
}
