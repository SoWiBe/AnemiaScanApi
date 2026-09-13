using AnemiaScanApi.Common.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Model for anemia scan data.
/// </summary>
public class AnemiaScan : BaseMongoModel
{
    [BsonElement("analysis_id")] public string AnalysisId { get; set; }
    /// <summary>
    /// The user ID associated with the scan.
    /// </summary>
    [BsonElement("user_id")] public string UserId { get; set; } = null!;
    /// <summary>
    /// The date when the scan was performed.
    /// </summary>
    [BsonElement("scan_date")] public DateTime ScanDate { get; set; }
    /// <summary>
    /// The hemoglobin level measured during the scan (CIELab regression —
    /// see AnemiaScanApi.ML.HemoglobinPrediction). Null when that model
    /// failed on this image (bad input) — the primary anemia classification
    /// below is unaffected either way.
    /// </summary>
    [BsonElement("hemoglobin_level")] public double? HemoglobinLevel { get; set; }
    /// <summary>
    /// Severity band for <see cref="HemoglobinLevel"/> (Non-Anemic/Mild/
    /// Moderate/Severe — see AnemiaScanApi.ML.SeverityBands). Null exactly
    /// when <see cref="HemoglobinLevel"/> is null.
    /// </summary>
    [BsonElement("severity")] public string? Severity { get; set; }
    /// <summary>
    /// Indicates whether the user is anemic based on the scan results.
    /// </summary>
    [BsonElement("is_anemic")] public bool IsAnemic { get; set; }
    /// <summary>
    /// The timestamp when the scan was created.
    /// </summary>
    [BsonElement("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The timestamp when the scan was last updated.
    /// </summary>
    [BsonElement("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The confidence level of the anemia detection.
    /// </summary>
    [BsonElement("confidence")] public double Confidence { get; set; }
    /// <summary>
    /// The type of image captured for the scan.
    /// </summary>
    [BsonElement("image_type")] public ImageType ImageType { get; set; } = ImageType.Conjunctiva;
    /// <summary>
    /// The unique identifier of the captured image in GridFS.
    /// </summary>
    [BsonElement("image_gridfs_id")] public ObjectId ImageGridFsId { get; set; }
    /// <summary>
    /// The unique identifier of the captured image in the system.
    /// </summary>
    [BsonElement("image_system_id")] public string ImageSystemId { get; set; }
    /// <summary>
    /// The version of the CIELab Hb-regression model used for this scan
    /// (see AnemiaScanApi.Common.Constants.ModelVersions). Null when
    /// <see cref="HemoglobinLevel"/> wasn't computed. Does not track the
    /// primary TF anemia classifier's version — that's a separate, older
    /// path that predates this field being populated.
    /// </summary>
    [BsonElement("model_version")] public string? ModelVersion { get; set; }
}