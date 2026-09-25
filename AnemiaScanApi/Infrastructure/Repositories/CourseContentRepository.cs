using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Core;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class CourseContentRepository(IMongoDatabase database, ILogger<CourseContentRepository> logger)
    : BaseMongoRepository<CourseContent>(database, MongoCollection.CourseContent, logger), ICourseContentRepository
{
    public async Task<CourseContent?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.CourseId == courseId)
            .FirstOrDefaultAsync(cancellationToken);
}
