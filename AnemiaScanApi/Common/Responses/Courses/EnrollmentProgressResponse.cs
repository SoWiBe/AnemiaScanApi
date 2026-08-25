using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Full progress view including per-day completion and Hb dynamics from checkpoint scans.
/// </summary>
public record EnrollmentProgressResponse(
    Guid EnrollmentId,
    Guid CourseId,
    string CourseTitle,
    int DurationDays,
    EnrollmentStatus Status,
    int CurrentStreak,
    int LongestStreak,
    DateTime EnrolledAt,
    DateTime? CompletedAt,
    IReadOnlyList<DayProgressItem> Days,
    IReadOnlyList<HemoglobinPoint> HemoglobinTimeline);

/// <summary>
/// Per-day completion info for the progress screen.
/// </summary>
public record DayProgressItem(
    int DayNumber,
    bool IsRescanCheckpoint,
    int TotalTasks,
    int CompletedTasks,
    bool IsCompleted,
    DateTime? CompletedAt,
    Guid? CheckpointScanId);

/// <summary>
/// One point on the Hb progress chart.
/// </summary>
public record HemoglobinPoint(int DayNumber, DateTime ScanDate, double? HemoglobinLevel, bool IsAnemic);
