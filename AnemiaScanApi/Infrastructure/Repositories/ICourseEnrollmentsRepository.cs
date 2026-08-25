using AnemiaScanApi.Common;
using AnemiaScanApi.Infrastructure.Core;

namespace AnemiaScanApi.Infrastructure.Repositories;

public interface ICourseEnrollmentsRepository : IMongoRepository<CourseEnrollment>
{
    /// <summary>
    /// Returns all enrollments (any status) for a user.
    /// </summary>
    Task<IEnumerable<CourseEnrollment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the enrollment for the given user + course, if any.
    /// </summary>
    Task<CourseEnrollment?> GetByUserAndCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken = default);
}
