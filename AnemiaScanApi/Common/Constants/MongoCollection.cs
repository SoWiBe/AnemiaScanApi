namespace AnemiaScanApi.Common.Constants;

/// <summary>
/// Constants for MongoDB collections.
/// </summary>
public static class MongoCollection
{
    /// <summary>
    /// Collection for user data.
    /// </summary>
    public const string Users = "Users";
    /// <summary>
    /// Collection for anemia scan data.
    /// </summary>
    public const string AnemiaScans = "AnemiaScans";
    /// <summary>
    /// Collection for course catalog entries.
    /// </summary>
    public const string Courses = "Courses";
    /// <summary>
    /// Collection for course content (days + tasks).
    /// </summary>
    public const string CourseContent = "CourseContent";
    /// <summary>
    /// Collection for user enrollments in courses with progress.
    /// </summary>
    public const string CourseEnrollments = "CourseEnrollments";
    /// <summary>
    /// Collection for course payment intents.
    /// </summary>
    public const string PaymentIntents = "PaymentIntents";
}