using AnemiaScanApi.Common.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Catalog entry for a recovery-program course.
/// Content lives separately in <see cref="CourseContent"/> so the catalog stays cheap to list.
/// </summary>
/// <remarks>
/// Extra elements are ignored: documents written before the payment layer was removed still
/// carry is_free/price_usdc/price_kzt/free_days_preview, and must stay readable.
/// </remarks>
[BsonIgnoreExtraElements]
public class Course : BaseMongoModel
{
    /// <summary>
    /// URL-friendly stable identifier (e.g. "basic-anti-anemia").
    /// </summary>
    [BsonElement("slug")] public string Slug { get; set; } = null!;

    /// <summary>
    /// Human-readable title shown in the catalog.
    /// </summary>
    [BsonElement("title")] public string Title { get; set; } = null!;

    /// <summary>
    /// Marketing/summary description for the catalog card.
    /// </summary>
    [BsonElement("description")] public string Description { get; set; } = null!;

    /// <summary>
    /// Intended audience — drives recommendation logic.
    /// </summary>
    [BsonElement("target_audience")] public TargetAudience TargetAudience { get; set; }

    /// <summary>
    /// Duration of the course in days.
    /// </summary>
    [BsonElement("duration_days")] public int DurationDays { get; set; }

    /// <summary>
    /// Publication status. Only <see cref="CourseContentStatus.Published"/> shows in the catalog.
    /// </summary>
    [BsonElement("content_status")] public CourseContentStatus ContentStatus { get; set; } = CourseContentStatus.Draft;

    /// <summary>
    /// Name of the doctor who signed off on the content. Required for Published.
    /// </summary>
    [BsonElement("doctor_reviewer_name")] public string? DoctorReviewerName { get; set; }

    /// <summary>
    /// Timestamp of publication.
    /// </summary>
    [BsonElement("published_at")] public DateTime? PublishedAt { get; set; }

    [BsonElement("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
