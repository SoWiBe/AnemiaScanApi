using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.ML;
using MongoDB.Bson;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Interface for ML analysis service operations.
/// </summary>
public interface IAnemiaAnalysisService
{
    Task<AnalyseAnemiaResponse> WriteAnalyseAsync(
        Guid userId, ClassifierDecision decision, byte[] image, CancellationToken cancellationToken);
    Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Страница истории сканов пользователя (P0 №8). Выборка строго по userId —
    /// идентификатор берётся из JWT, а не из запроса.
    /// </summary>
    Task<AnalysisHistoryResponse> GetHistoryAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default);
}