using MongoDB.Bson;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.ML;
using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Infrastructure.Services;

public class AnemiaAnalysisService(
    IAnemiaScansRepository anemiaScansRepository,
    IProfileService profileService,
    IImageCompressor imageCompressor,
    IOptions<LegalSettings> legalSettings,
    ILogger<AnemiaAnalysisService> logger)
    : BaseService<AnemiaAnalysisService>(logger), IAnemiaAnalysisService
{
    public async Task<(ObjectId, Guid)> SaveImageAsync(Guid analysisId, Guid userId, byte[] image,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Analyzing anemia for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);

        var imageId = Guid.NewGuid();
        // В GridFS кладём уменьшенную копию: оригинал с телефона — 2-3 МБ, а
        // предсказания уже посчитаны по исходным байтам выше по стеку (P0 №9).
        var storedImage = imageCompressor.CompressForStorage(image);
        var gridFsId = await anemiaScansRepository.SaveImageAsync(
            storedImage, $"anemia_scan_{imageId}",
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

    public async Task<AnalyseAnemiaResponse> WriteAnalyseAsync(
        Guid userId, float score, string predictionLabel, byte[] image,
        HemoglobinPrediction? hemoglobinPrediction, CancellationToken cancellationToken)
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
            UserId = userId.ToString(),
            // CIELab-регрессия (см. IHemoglobinPredictionService) — отдельный
            // путь от основного TF-классификатора выше; null, если он не
            // смог посчитать признаки для этого изображения.
            HemoglobinLevel = hemoglobinPrediction?.HemoglobinLevel,
            Severity = hemoglobinPrediction?.Severity,
            ModelVersion = hemoglobinPrediction is not null ? ModelVersions.CielabHemoglobinRegression : null,
            IsAnemic = isAnemia,
            ScanDate = scanDate
        };

        // сохраняем анализ в базе данных
        var createdAnemiaScan = await anemiaScansRepository.CreateAnemiaScanAsync(anemiaScan, cancellationToken);

        // записываем анализ в профиль пользователя
        await profileService.WriteAnalysisAsync(userId, createdAnemiaScan, cancellationToken);

        return new AnalyseAnemiaResponse
        (
            createdAnemiaScan.Id,
            createdAnemiaScan.Confidence,
            createdAnemiaScan.IsAnemic ? Sick.Anemia : Sick.Healthy,
            imageId,
            scanDate,
            createdAnemiaScan.HemoglobinLevel,
            createdAnemiaScan.Severity,
            // Дисклеймер в каждом ответе: вердикт без него на экран попасть не должен (P0 №12).
            string.IsNullOrWhiteSpace(legalSettings.Value.DisclaimerText)
                ? MedicalDisclaimer.Text
                : legalSettings.Value.DisclaimerText
        );
    }
}