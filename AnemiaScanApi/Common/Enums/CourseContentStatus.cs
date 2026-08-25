namespace AnemiaScanApi.Common.Enums;

/// <summary>
/// Publication lifecycle of a course's content.
/// </summary>
public enum CourseContentStatus
{
    /// <summary>
    /// Draft content, not yet reviewed. Not visible in the catalog.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Awaiting doctor review. Not visible in the catalog.
    /// </summary>
    DoctorReview = 1,

    /// <summary>
    /// Reviewed and published. Visible in the catalog.
    /// </summary>
    Published = 2
}
