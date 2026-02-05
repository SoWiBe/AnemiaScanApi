using MongoDB.Bson;

using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Models;

namespace AnemiaScanApi.Infrastructure.Repositories;

/// <summary>
/// Service for AnemiaScan-related MongoDB operations.
/// </summary>
public interface IAnemiaScansRepository : IMongoRepository<AnemiaScan>
{
    /// <summary>
    /// Saves an image to GridFS and associates it with an AnemiaScan.
    /// </summary>
    /// <param name="image">Image data.</param>
    /// <param name="filename">Image filename.</param>
    /// <param name="contentType">Image MIME type.</param>
    /// <param name="analysisId">AnemiaScan ID.</param>
    /// <param name="userId">User ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The GridFS ID of the saved image.</returns>
    Task<ObjectId> SaveImageAsync(
        byte[] image, string filename, string contentType,
        Guid analysisId, Guid userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads an image from GridFS.
    /// </summary>
    /// <param name="imageId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<byte[]> DownloadImageAsync(string imageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create anemia scan record.
    /// </summary>
    /// <param name="anemiaScan"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AnemiaScan> CreateAnemiaScanAsync(AnemiaScan anemiaScan, CancellationToken cancellationToken = default);
    Task<AnemiaScan> GetAnemiaScanAsync(string analysisId, CancellationToken cancellationToken = default);
}