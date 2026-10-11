using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Infrastructure.Courses;

/// <summary>
/// Файл курса в том виде, в каком его сдаёт автор контента: карточка каталога
/// и все дни в одном JSON. Формат описан в docs/course-content-schema.md,
/// машинная схема — docs/schemas/course.schema.json.
///
/// Все поля nullable намеренно: отсутствующее обязательное поле должно дать
/// автору понятную ошибку «не заполнено», а не исключение десериализатора, и
/// все ошибки файла должны прийти одним списком, а не по одной за запуск.
/// </summary>
public sealed class CourseDocument
{
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public TargetAudience? TargetAudience { get; init; }
    public int? DurationDays { get; init; }
    public CourseContentStatus ContentStatus { get; init; } = CourseContentStatus.Draft;
    public string? DoctorReviewerName { get; init; }
    public List<CourseDayDocument>? Days { get; init; }
}

public sealed class CourseDayDocument
{
    public int? DayNumber { get; init; }
    public string? Theory { get; init; }
    public bool IsRescanCheckpoint { get; init; }
    public List<CourseTaskDocument>? Tasks { get; init; }
}

public sealed class CourseTaskDocument
{
    /// <summary>
    /// Автор его не заполняет: id проставляет импортёр и сохраняет между
    /// импортами. Поле есть для редкого случая, когда задачи в идущем курсе
    /// нужно переставить, не потеряв отметки пользователей.
    /// </summary>
    public Guid? Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}
