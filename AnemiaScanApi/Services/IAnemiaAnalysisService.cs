using AnemiaScanApi.Models.Responses;
using MongoDB.Bson;

namespace AnemiaScanApi.Services;

/// <summary>
/// Interface for ML analysis service operations.
/// </summary>
public interface IAnemiaAnalysisService
{
    /// <summary>
    /// Analyzes an image for anemia detection.
    /// </summary>
    /// <param name="imageId">Image ID.</param>
    /// <param name="analysisId">AnemiaScan ID.</param>
    /// <param name="userId">User ID.</param>
    /// <param name="image">Image data for analysis.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The GridFS ID of the saved image.</returns>
    Task<(ObjectId, Guid)> SaveImageAsync(Guid imageId, Guid analysisId, Guid userId, byte[] image, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves an image for an analysis.
    /// </summary>
    /// <param name="analysisId">AnemiaScan ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The image data.</returns>
    Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Analyzes an image for anemia detection.
    /// </summary>
    /// <param name="imageId">Image ID.</param>
    /// <param name="imageSystemId">Image system ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Anemia analysis response.</returns>
    Task<AnalyseAnemiaResponse> AnalyzeAnemiaAsync(ObjectId imageId, Guid imageSystemId, CancellationToken cancellationToken = default);
}