using AnemiaScanApi.Models.Responses;
using MongoDB.Bson;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Interface for ML analysis service operations.
/// </summary>
public interface IAnemiaAnalysisService
{
    Task<AnalyseAnemiaResponse> WriteAnalyseAsync(Guid userId, float score, string predictionLabel, byte[] image, CancellationToken cancellationToken);
    Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default);
}