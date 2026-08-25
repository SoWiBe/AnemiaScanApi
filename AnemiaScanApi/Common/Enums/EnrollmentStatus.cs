namespace AnemiaScanApi.Common.Enums;

/// <summary>
/// Status of a user's enrollment in a course.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>
    /// Enrollment is active and the user is progressing through the course.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The user completed all days of the course.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The user explicitly abandoned or was auto-marked as inactive.
    /// </summary>
    Abandoned = 2
}
