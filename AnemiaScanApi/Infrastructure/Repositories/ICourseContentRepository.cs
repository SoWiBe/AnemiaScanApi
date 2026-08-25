using AnemiaScanApi.Common;
using AnemiaScanApi.Infrastructure.Core;

namespace AnemiaScanApi.Infrastructure.Repositories;

public interface ICourseContentRepository : IMongoRepository<CourseContent>
{
    /// <summary>
    /// Returns the content document for a given course, or null if none exists yet.
    /// </summary>
    Task<CourseContent?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default);
}
