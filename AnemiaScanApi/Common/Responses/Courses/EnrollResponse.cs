namespace AnemiaScanApi.Common.Responses.Courses;

/// <summary>
/// Result of a successful enrollment.
/// </summary>
public record EnrollResponse(Guid EnrollmentId, Guid CourseId, string CourseSlug);
