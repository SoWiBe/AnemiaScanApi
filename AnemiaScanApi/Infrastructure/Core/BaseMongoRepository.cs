using AnemiaScanApi.Models;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
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

    protected BaseMongoRepository(IOptions<MongoDbSettings> mongoDbSettings, string collectionName, ILogger logger)
    {
        var client = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoDbSettings.Value.DatabaseName);
        
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
    
    public virtual Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
        => Collection
            .InsertOneAsync(entity, cancellationToken: cancellationToken)
            .ContinueWith(_ => entity, cancellationToken);
      
    public virtual Task<T> UpdateAsync(Guid id, T entity, CancellationToken cancellationToken = default)
        => Collection
            .ReplaceOneAsync(x => x.Id == id, entity, cancellationToken: cancellationToken)
            .ContinueWith(_ => entity, cancellationToken);

    public virtual Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => Collection
            .DeleteOneAsync(x => x.Id == id, cancellationToken: cancellationToken)
            .ContinueWith(_ => true, cancellationToken);
}