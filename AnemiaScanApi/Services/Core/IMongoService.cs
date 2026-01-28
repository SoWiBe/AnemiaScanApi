namespace AnemiaScanApi.Services.Core;

/// <summary>
/// Interface for MongoDB service operations.
/// </summary>
/// <typeparam name="T">The type of entity to work with.</typeparam>
public interface IMongoService<T>
{
    public Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default);
    public Task<T> UpdateAsync(Guid id, T entity, CancellationToken cancellationToken = default);
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}