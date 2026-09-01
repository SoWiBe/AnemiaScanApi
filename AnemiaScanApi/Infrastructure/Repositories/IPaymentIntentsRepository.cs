using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Core;

namespace AnemiaScanApi.Infrastructure.Repositories;

public interface IPaymentIntentsRepository : IMongoRepository<PaymentIntent>
{
    /// <summary>
    /// Returns every intent the user has on a course, newest first.
    /// </summary>
    Task<IEnumerable<PaymentIntent>> GetByUserAndCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks an intent up by its Solana Pay reference key.
    /// </summary>
    Task<PaymentIntent?> GetByReferenceAsync(string referenceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> intents still in <see cref="PaymentStatus.Pending"/>,
    /// oldest first — the reconciliation worker's work queue.
    /// </summary>
    Task<IEnumerable<PaymentIntent>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
}
