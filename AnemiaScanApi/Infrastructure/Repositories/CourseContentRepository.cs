using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Core;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class CourseContentRepository(IOptions<MongoDbSettings> mongoDbSettings, ILogger<CourseContentRepository> logger)
    : BaseMongoRepository<CourseContent>(mongoDbSettings, MongoCollection.CourseContent, logger), ICourseContentRepository
{
    public async Task<CourseContent?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.CourseId == courseId)
            .FirstOrDefaultAsync(cancellationToken);
}
