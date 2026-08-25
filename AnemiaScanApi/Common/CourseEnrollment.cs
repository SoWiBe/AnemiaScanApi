using AnemiaScanApi.Common.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// A user's enrollment in a course, including per-day progress and streak state.
/// </summary>
public class CourseEnrollment : BaseMongoModel
{
    /// <summary>
    /// User who enrolled.
    /// </summary>
    [BsonElement("user_id")] public Guid UserId { get; set; }

    /// <summary>
    /// Course this enrollment is for.
    /// </summary>
    [BsonElement("course_id")] public Guid CourseId { get; set; }

    [BsonElement("enrolled_at")] public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    [BsonElement("status")] public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    /// <summary>
    /// Current consecutive-day streak.
    /// </summary>
    [BsonElement("current_streak")] public int CurrentStreak { get; set; }

    /// <summary>
    /// All-time best streak in this enrollment.
    /// </summary>
    [BsonElement("longest_streak")] public int LongestStreak { get; set; }

    /// <summary>
    /// UTC date of the last day the user completed at least one task or marked a day done.
    /// Kept as a date-only DateTime (midnight UTC) so streak comparison is by calendar day.
    /// </summary>
    [BsonElement("last_activity_date")] public DateTime? LastActivityDate { get; set; }

    /// <summary>
    /// Per-day progress. Missing entries mean the day was not started.
    /// </summary>
    [BsonElement("days")] public List<DayCompletion> Days { get; set; } = new();

    /// <summary>
    /// Payment intent that unlocked this enrollment (Phase 2+). Null for free enrollments.
    /// </summary>
    [BsonElement("paid_intent_id")] public Guid? PaidIntentId { get; set; }

    /// <summary>
    /// Set when Status transitions to Completed.
    /// </summary>
    [BsonElement("completed_at")] public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Per-day progress within an enrollment.
/// </summary>
public class DayCompletion
{
    /// <summary>
    /// 1-based day number this record corresponds to.
    /// </summary>
    [BsonElement("day_number")] public int DayNumber { get; set; }

    /// <summary>
    /// IDs of <see cref="CourseTask"/> entries marked done for this day.
    /// </summary>
    [BsonElement("completed_task_ids")] public List<Guid> CompletedTaskIds { get; set; } = new();

    /// <summary>
    /// Set once all tasks of the day (and, if applicable, the rescan checkpoint) are done.
    /// </summary>
    [BsonElement("completed_at")] public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// ID of the <see cref="AnemiaScan"/> attached for a rescan-checkpoint day.
    /// </summary>
    [BsonElement("checkpoint_scan_id")] public Guid? CheckpointScanId { get; set; }
}
