using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Full course card including day previews (only titles + task counts, not full theory).
/// </summary>
public record CourseDetailsResponse(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    TargetAudience TargetAudience,
    int DurationDays,
    bool IsFree,
    decimal PriceUsdc,
    decimal PriceKzt,
    int FreeDaysPreview,
    string? DoctorReviewerName,
    IReadOnlyList<CourseDayPreview> DayPreviews);

/// <summary>
/// Preview of a day shown on the course details screen (no theory body).
/// </summary>
public record CourseDayPreview(int DayNumber, int TaskCount, bool IsRescanCheckpoint);
