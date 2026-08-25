using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Lightweight catalog card used in listings.
/// </summary>
public record CourseListItemResponse(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    TargetAudience TargetAudience,
    int DurationDays,
    bool IsFree,
    decimal PriceUsdc,
    decimal PriceKzt,
    int FreeDaysPreview);
