using AnemiaScanApi.Common;
using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Core;

/// <summary>
/// Base implementation of IMongoService.
/// </summary>
/// <typeparam name="T">The type of entity to work with.</typeparam>
public abstract class BaseMongoRepository<T> : IMongoRepository<T> where T : BaseMongoModel
{
    protected readonly IMongoCollection<T> Collection;
    protected readonly ILogger Logger;

    /// <param name="database">
    /// Синглтон <see cref="IMongoDatabase"/> из DI (см. ServicesExtensions.AddMongoDb).
    /// Репозитории Scoped, но клиент и база — общие на всё приложение: свой
    /// <c>MongoClient</c> на репозиторий означал бы TLS-хендшейк и новый пул
    /// соединений на каждый HTTP-запрос (P0 №2 в docs/plans/MVP_PLAN.md).
    /// </param>
    protected BaseMongoRepository(IMongoDatabase database, string collectionName, ILogger logger)
    {
        Collection = database.GetCollection<T>(collectionName);
        Logger = logger;
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Collection
            .Find(_ => true)
            .ToListAsync(cancellationToken);

    public virtual async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Collection
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    
    // Ниже — именно await, а не ContinueWith(_ => entity): продолжение без
    // проверки статуса выполнялось и после упавшей записи, исключение терялось,
    // и вызывающий код получал «успех» при несохранённом документе.
    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(Guid id, T entity, CancellationToken cancellationToken = default)
    {
        await Collection.ReplaceOneAsync(x => x.Id == id, entity, cancellationToken: cancellationToken);
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Collection.DeleteOneAsync(x => x.Id == id, cancellationToken: cancellationToken);
        return true;
    }
}
