namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Card for the "today" screen — theory + tasks + completion state + checkpoint flag.
/// </summary>
public record TodayDayResponse(
    Guid EnrollmentId,
    Guid CourseId,
    int DayNumber,
    string Theory,
    bool IsRescanCheckpoint,
    Guid? CheckpointScanId,
    IReadOnlyList<TodayTask> Tasks,
    bool IsDayCompleted);

/// <summary>
/// One task shown on the "today" screen with its completion flag for the current enrollment.
/// </summary>
public record TodayTask(Guid Id, string Title, string? Description, bool IsCompleted);
