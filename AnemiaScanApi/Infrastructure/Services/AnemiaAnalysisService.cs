using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Models;
using AnemiaScanApi.Models.Responses;
using AnemiaScanApi.Infrastructure.Services.Core;
using MongoDB.Bson;

namespace AnemiaScanApi.Services;

public class AnemiaAnalysisService(
    IAnemiaScansRepository anemiaScansRepository, 
    ILogger<AnemiaAnalysisService> logger)
    : BaseService<AnemiaAnalysisService>(logger), IAnemiaAnalysisService
{
    public async Task<(ObjectId, Guid)> SaveImageAsync(Guid analysisId, Guid userId, byte[] image,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Analyzing anemia for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);

        var imageId = Guid.NewGuid();
        var gridFsId = await anemiaScansRepository.SaveImageAsync(
            image, $"anemia_scan_{imageId}",
            "image/jpeg",
            analysisId, 
            userId, 
            cancellationToken);
        
        Logger.LogInformation("Anemia analysis completed for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);
        Logger.LogInformation("Anemia analysis result: Confidence {Confidence}, ObjectId {ObjectId}", 0.85, gridFsId);
        return (gridFsId, imageId);
    }
    
    public async Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        var anemiaScan = await anemiaScansRepository.GetAnemiaScanAsync(analysisId, cancellationToken);
        return await anemiaScansRepository.DownloadImageAsync(anemiaScan.ImageSystemId, cancellationToken);
    }

    public async Task<AnalyseAnemiaResponse> WriteAnalyseAsync(Guid userId, float score, string predictionLabel, byte[] image, CancellationToken cancellationToken)
    {
        var analysisId = Guid.NewGuid();
        var scanDate = DateTime.UtcNow;

        var (gridFsId, imageId) = await SaveImageAsync(analysisId, userId, image, cancellationToken);
        var isAnemia = predictionLabel == "Low_Hb";

        var anemiaScan = new AnemiaScan
        {
            AnalysisId = analysisId.ToString(),
            ImageSystemId = imageId.ToString(),
            ImageGridFsId = gridFsId,
            Confidence = score,
            UserId = Guid.NewGuid().ToString(),
            HemoglobinLevel = null, // у нас нет такой информации
            IsAnemic = isAnemia,
            ScanDate = scanDate
        };

        var createdAnemiaScan = await anemiaScansRepository.CreateAnemiaScanAsync(anemiaScan, cancellationToken);
        return new AnalyseAnemiaResponse
        (
            createdAnemiaScan.Id,
            createdAnemiaScan.Confidence,
            createdAnemiaScan.IsAnemic ? Models.Enums.Sick.Anemia : Models.Enums.Sick.Healthy,
            imageId,
            scanDate
        );
    }
}