using AnemiaScanApi.Common.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Catalog entry for a recovery-program course.
/// Content lives separately in <see cref="CourseContent"/> so the catalog stays cheap to list.
/// </summary>
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
    /// True in Phase 1 for all courses. Flip to false in Phase 2 with a price set.
    /// </summary>
    [BsonElement("is_free")] public bool IsFree { get; set; } = true;

    /// <summary>
    /// Price in USDC for Solana Pay (used in Phase 2+).
    /// </summary>
    [BsonElement("price_usdc")] public decimal PriceUsdc { get; set; }

    /// <summary>
    /// Price in KZT for Kaspi/card (used in Phase 2+).
    /// </summary>
    [BsonElement("price_kzt")] public decimal PriceKzt { get; set; }

    /// <summary>
    /// Number of freemium preview days accessible without payment (Phase 2+).
    /// </summary>
    [BsonElement("free_days_preview")] public int FreeDaysPreview { get; set; }

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
