using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Models;
using AnemiaScanApi.Models.Responses;
using AnemiaScanApi.Services.Core;
using MongoDB.Bson;

namespace AnemiaScanApi.Services;

/// <summary>
/// Service for ML analysis operations.
/// </summary>
public class AnemiaAnalysisService(IAnemiaScansRepository anemiaScansRepository, ILogger<AnemiaAnalysisService> logger) 
    : BaseService<AnemiaAnalysisService>(logger), IAnemiaAnalysisService
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
    public async Task<(ObjectId, Guid)> SaveImageAsync(Guid imageId, Guid analysisId, Guid userId, byte[] image,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Analyzing anemia for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);
        
        var gridFsId = await anemiaScansRepository.SaveImageAsync(
            image, $"anemia_scan_{imageId}", //TODO: Optimization and remove from params, create configuration
            "image/jpeg", //TODO: Optimization and remove from params, create configuration
            analysisId, 
            userId, 
            cancellationToken);
        
        Logger.LogInformation("Anemia analysis completed for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);
        Logger.LogInformation("Anemia analysis result: Confidence {Confidence}, ObjectId {ObjectId}", 0.85, gridFsId);
        return (gridFsId, imageId);
    }
    
    /// <summary>
    /// Retrieves an image for an analysis.
    /// </summary>
    /// <param name="analysisId">AnemiaScan ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The image data.</returns>
    public async Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        var anemiaScan = await anemiaScansRepository.GetAnemiaScanAsync(analysisId, cancellationToken);
        return await anemiaScansRepository.DownloadImageAsync(anemiaScan.ImageSystemId, cancellationToken);
    }

    /// <summary>
    /// Analyzes an image for anemia detection.
    /// </summary>
    /// <param name="imageId">Image ID.</param>
    /// <param name="imageSystemId">Image system ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Anemia analysis response.</returns>
    public async Task<AnalyseAnemiaResponse> AnalyzeAnemiaAsync(ObjectId imageId, Guid imageSystemId, CancellationToken cancellationToken = default)
    {
        //TODO: Call ML service for anemia analysis
        //_mlService.AnalyzeAnemia(imageId);
        
        Logger.LogInformation("Anemia analysis completed for image ID {ImageId}", imageId);
        
        var anemiaScan = new AnemiaScan
        {
            AnalysisId = Guid.NewGuid().ToString(),
            ImageSystemId = imageSystemId.ToString(),
            ImageGridFsId = imageId,
            Confidence = 0.85,
            UserId = Guid.NewGuid().ToString(),
            HemoglobinLevel = 12.5,
            IsAnemic = true,
            ScanDate = DateTime.UtcNow
        };
        
        var createdAnemiaScan = await anemiaScansRepository.CreateAnemiaScanAsync(anemiaScan, cancellationToken);
        return new AnalyseAnemiaResponse 
        {
            AnalysisId = Guid.Parse(createdAnemiaScan.AnalysisId),
            Confidence = createdAnemiaScan.Confidence,
            ImageSystemId = Guid.Parse(createdAnemiaScan.ImageSystemId)
        };
    }
}