namespace AnemiaScanApi.Services.Core;

/// <summary>
/// Interface for MongoDB service operations.
/// </summary>
/// <typeparam name="T">The type of entity to work with.</typeparam>
public interface IMongoService<T>
{
    public Task<IEnumerable<T>> GetAllAsync();
    public Task<T> GetByIdAsync(Guid id);
    public Task<T> CreateAsync(T entity);
    public Task<T> UpdateAsync(Guid id, T entity);
    public Task<bool> DeleteAsync(Guid id);
}