using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Core;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class CourseEnrollmentsRepository(IMongoDatabase database, ILogger<CourseEnrollmentsRepository> logger)
    : BaseMongoRepository<CourseEnrollment>(database, MongoCollection.CourseEnrollments, logger), ICourseEnrollmentsRepository
{
    public async Task<IEnumerable<CourseEnrollment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<CourseEnrollment?> GetByUserAndCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.UserId == userId && x.CourseId == courseId)
            .FirstOrDefaultAsync(cancellationToken);
}
