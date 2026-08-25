using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Content of a course — its days and per-day tasks.
/// Stored separately from <see cref="Course"/> so the catalog stays cheap to list.
/// </summary>
public class CourseContent : BaseMongoModel
{
    /// <summary>
    /// Course this content belongs to.
    /// </summary>
    [BsonElement("course_id")] public Guid CourseId { get; set; }

    /// <summary>
    /// Ordered days of the course.
    /// </summary>
    [BsonElement("days")] public List<CourseDay> Days { get; set; } = new();

    [BsonElement("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One day of a course — theory + tasks + optional rescan checkpoint.
/// </summary>
public class CourseDay
{
    /// <summary>
    /// 1-based day number within the course.
    /// </summary>
    [BsonElement("day_number")] public int DayNumber { get; set; }

    /// <summary>
    /// Theory text in Markdown.
    /// </summary>
    [BsonElement("theory")] public string Theory { get; set; } = null!;

    /// <summary>
    /// Actionable tasks the user should complete today.
    /// </summary>
    [BsonElement("tasks")] public List<CourseTask> Tasks { get; set; } = new();

    /// <summary>
    /// When true, the user is asked to attach a fresh <see cref="AnemiaScan"/> to this day.
    /// </summary>
    [BsonElement("is_rescan_checkpoint")] public bool IsRescanCheckpoint { get; set; }
}

/// <summary>
/// One task within a day of a course.
/// </summary>
public class CourseTask
{
    /// <summary>
    /// Stable ID of the task, referenced from enrollment progress.
    /// </summary>
    [BsonElement("id")] public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Short title (e.g. "Съесть говяжью печень").
    /// </summary>
    [BsonElement("title")] public string Title { get; set; } = null!;

    /// <summary>
    /// Optional longer description.
    /// </summary>
    [BsonElement("description")] public string? Description { get; set; }
}
