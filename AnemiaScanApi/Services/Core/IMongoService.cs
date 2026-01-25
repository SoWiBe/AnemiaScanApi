using MongoDB.Bson;

namespace AnemiaScanApi.Services;

/// <summary>
/// Interface for MongoDB service operations.
/// </summary>
/// <typeparam name="T">The type of entity to work with.</typeparam>
public interface IMongoService<T>
{
    public Task<IEnumerable<T>> GetAllAsync();
    public Task<T> GetByIdAsync(ObjectId id);
    public Task<T> CreateAsync(T entity);
    public Task<T> UpdateAsync(ObjectId id, T entity);
    public Task<bool> DeleteAsync(ObjectId id);
}