using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
 
using AnemiaScanApi.Infrastructure.Core;
using MongoDB.Bson;

namespace AnemiaScanApi.Infrastructure.Repositories;

/// <summary>
/// Service for AnemiaScan-related MongoDB operations.
/// </summary>
public class AnemiaScansRepository : BaseMongoRepository<AnemiaScan>, IAnemiaScansRepository
{
    /// <summary>
    /// GridFS bucket for storing images.
    /// </summary>
    private readonly IGridFSBucket _gridFsBucket;

    /// <summary>
    /// Service for AnemiaScan-related MongoDB operations.
    /// </summary>
    /// <param name="database">Синглтон базы из DI (см. ServicesExtensions.AddMongoDb).</param>
    /// <param name="gridFsBucket">Синглтон GridFS-бакета поверх той же базы.</param>
    /// <param name="logger"></param>
    public AnemiaScansRepository(IMongoDatabase database, IGridFSBucket gridFsBucket, ILogger<AnemiaScansRepository> logger) 
        : base(database, MongoCollection.AnemiaScans, logger)
    {
        _gridFsBucket = gridFsBucket;
    }

    //TODO: Transfer workflow with Images to ImagesRepository
    #region Image operations
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
    public async Task<ObjectId> SaveImageAsync(byte[] image, string filename, string contentType, Guid analysisId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                { "analysisId", analysisId.ToString() },
                { "userId", userId.ToString() },
                { "contentType", contentType }
            }
        };
        
        return await _gridFsBucket.UploadFromBytesAsync(filename, image, options, cancellationToken);
    }

    /// <summary>
    /// Downloads an image from GridFS.
    /// </summary>
    /// <param name="imageId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<byte[]> DownloadImageAsync(string imageId, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Downloading image for image ID {ImageId}", imageId);
        var imageBytes = await _gridFsBucket.DownloadAsBytesByNameAsync($"anemia_scan_{imageId}", cancellationToken: cancellationToken);
        Logger.LogInformation("Image for image ID {ImageId} downloaded successfully", imageId);
        return imageBytes;
    }
    
    #endregion
    
    #region AnemiaScan operations

    /// <summary>
    /// Creates an AnemiaScan record.
    /// </summary>
    /// <param name="anemiaScan"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<AnemiaScan> CreateAnemiaScanAsync(AnemiaScan anemiaScan, CancellationToken cancellationToken = default)
        => await CreateAsync(anemiaScan, cancellationToken);

    //TODO: Change to Guid param
    public async Task<AnemiaScan> GetAnemiaScanAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Retrieving AnemiaScan for analysis ID {AnalysisId}", analysisId);
        var anemiaScan = await Collection
            .Find(x => x.AnalysisId == analysisId)
            .FirstOrDefaultAsync(cancellationToken);
        Logger.LogInformation("AnemiaScan for analysis ID {AnalysisId} retrieved successfully", analysisId);
        return anemiaScan;
    }
    
    /// <summary>
    /// Сканы пользователя, новые сверху. UserId в документе хранится строкой,
    /// поэтому сравнение идёт со строковым представлением Guid.
    /// </summary>
    public async Task<IReadOnlyList<AnemiaScan>> GetByUserAsync(Guid userId, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        var ownerId = userId.ToString();
        return await Collection
            .Find(x => x.UserId == ownerId)
            .SortByDescending(x => x.ScanDate)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    public Task<long> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var ownerId = userId.ToString();
        return Collection.CountDocumentsAsync(x => x.UserId == ownerId, cancellationToken: cancellationToken);
    }

    public async Task<AnemiaScan?> GetOwnedAsync(Guid scanId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ownerId = userId.ToString();
        return await Collection
            .Find(x => x.Id == scanId && x.UserId == ownerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SetLabHemoglobinAsync(Guid scanId, Guid userId, double hemoglobin, DateTime measuredAt,
        CancellationToken cancellationToken = default)
    {
        var ownerId = userId.ToString();
        var update = Builders<AnemiaScan>.Update
            .Set(x => x.LabHemoglobin, hemoglobin)
            .Set(x => x.LabMeasuredAt, measuredAt)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await Collection.UpdateOneAsync(
            x => x.Id == scanId && x.UserId == ownerId, update, cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    #endregion
}
