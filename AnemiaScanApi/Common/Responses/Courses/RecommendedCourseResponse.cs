namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Recommended course after a scan. <see cref="Reason"/> is a short user-facing hint
/// ("По результатам последнего скана" / "Для беременных").
/// </summary>
public record RecommendedCourseResponse(CourseListItemResponse Course, string Reason);
