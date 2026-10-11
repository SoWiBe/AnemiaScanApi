using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Core;

namespace AnemiaScanApi.Infrastructure.Repositories;

public interface ICoursesRepository : IMongoRepository<Course>
{
    /// <summary>
    /// Returns only courses with <see cref="CourseContentStatus.Published"/>.
    /// </summary>
    Task<IEnumerable<Course>> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a published course by its slug, or null if not found or not published.
    /// </summary>
    Task<Course?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Курс по slug в любом статусе, включая черновики. Нужен импортёру: каталог
    /// черновиков не видит, а повторный импорт должен найти и их.
    /// </summary>
    Task<Course?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
