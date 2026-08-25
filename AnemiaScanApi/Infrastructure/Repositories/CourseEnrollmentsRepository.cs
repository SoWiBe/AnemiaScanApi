using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class CourseEnrollmentsRepository(IOptions<MongoDbSettings> mongoDbSettings, ILogger<CourseEnrollmentsRepository> logger)
    : BaseMongoRepository<CourseEnrollment>(mongoDbSettings, MongoCollection.CourseEnrollments, logger), ICourseEnrollmentsRepository
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
