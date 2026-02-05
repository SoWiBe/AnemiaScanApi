using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
 
using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Models;
using AnemiaScanApi.Models.Constants;
using AnemiaScanApi.Settings;
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
    private readonly GridFSBucket _gridFsBucket;

    /// <summary>
    /// Service for AnemiaScan-related MongoDB operations.
    /// </summary>
    /// <param name="mongoDbSettings"></param>
    /// <param name="logger"></param>
    public AnemiaScansRepository(IOptions<MongoDbSettings> mongoDbSettings, ILogger<AnemiaScansRepository> logger) 
        : base(mongoDbSettings, MongoCollection.AnemiaScans, logger)
    {
        var client = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _gridFsBucket = new GridFSBucket(database);
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
    
    #endregion  
}