using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class PaymentIntentsRepository(IOptions<MongoDbSettings> mongoDbSettings, ILogger<PaymentIntentsRepository> logger)
    : BaseMongoRepository<PaymentIntent>(mongoDbSettings, MongoCollection.PaymentIntents, logger), IPaymentIntentsRepository
{
    public async Task<IEnumerable<PaymentIntent>> GetByUserAndCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.UserId == userId && x.CourseId == courseId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<PaymentIntent?> GetByReferenceAsync(string referenceKey, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.ReferenceKey == referenceKey)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<PaymentIntent>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.Status == PaymentStatus.Pending)
            .SortBy(x => x.CreatedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
}
