using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Card for the "my active courses" screen.
/// </summary>
public record EnrollmentSummaryResponse(
    Guid EnrollmentId,
    Guid CourseId,
    string CourseSlug,
    string CourseTitle,
    int DurationDays,
    EnrollmentStatus Status,
    int CurrentDayNumber,
    int CompletedDaysCount,
    int CurrentStreak,
    int LongestStreak,
    DateTime EnrolledAt);
