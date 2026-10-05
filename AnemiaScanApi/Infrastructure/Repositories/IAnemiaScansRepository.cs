using AnemiaScanApi.Common;
using MongoDB.Bson;

using AnemiaScanApi.Infrastructure.Core;

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

    /// <summary>
    /// Сканы пользователя, новые сверху, страницей (P0 №8).
    /// </summary>
    Task<IReadOnlyList<AnemiaScan>> GetByUserAsync(Guid userId, int skip, int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сколько всего сканов у пользователя — для постраничной навигации.
    /// </summary>
    Task<long> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Скан по идентификатору, но только если он принадлежит пользователю.
    /// Чужой скан и несуществующий неразличимы — оба null.
    /// </summary>
    Task<AnemiaScan?> GetOwnedAsync(Guid scanId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает Hb из анализа крови в скан пользователя. Владелец проверяется
    /// в самом фильтре, а не отдельным чтением: между проверкой и записью чужой
    /// скан не проскочит. False — такого скана у пользователя нет.
    /// </summary>
    Task<bool> SetLabHemoglobinAsync(Guid scanId, Guid userId, double hemoglobin, DateTime measuredAt,
        CancellationToken cancellationToken = default);
}