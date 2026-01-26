using AnemiaScanApi.Models;
using AnemiaScanApi.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AnemiaScanApi.Services.Core;

/// <summary>
/// Base implementation of IMongoService.
/// </summary>
/// <typeparam name="T">The type of entity to work with.</typeparam>
public abstract class BaseMongoService<T> : IMongoService<T> where T : BaseMongoModel
{
    protected readonly IMongoCollection<T> Collection;

    protected BaseMongoService(IOptions<MongoDbSettings> mongoDbSettings, string collectionName)
    {
        var client = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var database = client.GetDatabase(mongoDbSettings.Value.DatabaseName);
        
        Collection = database.GetCollection<T>(collectionName);        
    }

    /// <summary>
    /// Retrieves all entities from the collection.
    /// </summary>
    /// <returns>All entities in the collection.</returns>
    public virtual async Task<IEnumerable<T>> GetAllAsync()
        => await Collection.Find(_ => true).ToListAsync();

    /// <summary>
    /// Retrieves an entity by its Guid.
    /// </summary>
    /// <param name="id">The Guid of the entity.</param>
    /// <returns>The entity with the specified Guid.</returns>
 
    public virtual async Task<T> GetByIdAsync(Guid id)
        => await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    
    /// <summary>
    /// Creates a new entity in the collection.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <returns>The created entity.</returns>
    public virtual Task<T> CreateAsync(T entity)
        => Collection.InsertOneAsync(entity).ContinueWith(_ => entity);
    
    /// <summary>
    /// Updates an existing entity in the collection.
    /// </summary>
    /// <param name="id">The Guid of the entity to update.</param>
    /// <param name="entity">The updated entity.</param>
    /// <returns>The updated entity.</returns>    
    public virtual Task<T> UpdateAsync(Guid id, T entity)
        => Collection.ReplaceOneAsync(x => x.Id == id, entity).ContinueWith(_ => entity);

    
    /// <summary>
    /// Deletes an entity from the collection.
    /// </summary>
    /// <param name="id">The Guid of the entity to delete.</param>
    /// <returns>True if the entity was deleted, false otherwise.</returns>
    public virtual Task<bool> DeleteAsync(Guid id)
        => Collection.DeleteOneAsync(x => x.Id == id).ContinueWith(_ => true);
}