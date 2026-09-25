using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Core;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

public class CoursesRepository(IMongoDatabase database, ILogger<CoursesRepository> logger)
    : BaseMongoRepository<Course>(database, MongoCollection.Courses, logger), ICoursesRepository
{
    public async Task<IEnumerable<Course>> GetPublishedAsync(CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.ContentStatus == CourseContentStatus.Published)
            .ToListAsync(cancellationToken);

    public async Task<Course?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.Slug == slug && x.ContentStatus == CourseContentStatus.Published)
            .FirstOrDefaultAsync(cancellationToken);
}
