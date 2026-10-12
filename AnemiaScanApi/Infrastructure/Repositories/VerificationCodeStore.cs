using System.Security.Cryptography;
using System.Text;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;

using MongoDB.Driver;

namespace AnemiaScanApi.Infrastructure.Repositories;

/// <summary>
/// Коды подтверждения в MongoDB — переживают рестарт сервиса.
///
/// Синглтон: держит только коллекцию из общего <see cref="IMongoDatabase"/> и
/// флаг «индекс создан», так что создавать его на каждый запрос незачем.
/// </summary>
public class VerificationCodeStore(
    IMongoDatabase database,
    ILogger<VerificationCodeStore> logger,
    TimeProvider? timeProvider = null) : IVerificationCodeStore
{
    /// <summary>Сколько действует код. Столько же он жил в кэше до переезда в базу.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IMongoCollection<VerificationCode> _collection =
        database.GetCollection<VerificationCode>(MongoCollection.VerificationCodes);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private volatile bool _indexEnsured;

    public async Task SaveAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        var document = new VerificationCode
        {
            Email = Normalize(email),
            CodeHash = Hash(code),
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        };

        await _collection.ReplaceOneAsync(
            x => x.Email == document.Email, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<VerificationCodeCheck> CheckAsync(string email, string? code,
        CancellationToken cancellationToken = default)
    {
        var key = Normalize(email);
        var now = _time.GetUtcNow().UtcDateTime;

        // Срок сверяем сами: TTL-индекс удаляет документы фоновой задачей раз в
        // минуту, и без этого условия истёкший код жил бы ещё до минуты.
        var stored = await _collection
            .Find(x => x.Email == key && x.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null) return VerificationCodeCheck.Missing;
        if (string.IsNullOrWhiteSpace(code)) return VerificationCodeCheck.Mismatch;

        // Сравнение за постоянное время: по длительности ответа нельзя понять,
        // сколько первых символов кода угадано.
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(stored.CodeHash), Encoding.ASCII.GetBytes(Hash(code)));

        return matches ? VerificationCodeCheck.Valid : VerificationCodeCheck.Mismatch;
    }

    public Task RemoveAsync(string email, CancellationToken cancellationToken = default)
    {
        var key = Normalize(email);
        return _collection.DeleteOneAsync(x => x.Email == key, cancellationToken);
    }

    /// <summary>
    /// TTL-индекс: Mongo сама удаляет документ, когда наступает expires_at.
    ///
    /// Создаётся при первом сохранении кода, а не при старте: сервис должен
    /// подниматься и когда база ещё не ответила. Неудача не мешает работе — срок
    /// кода проверяется при чтении, — поэтому она только логируется, а попытка
    /// повторится при следующем сохранении.
    /// </summary>
    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexEnsured) return;

        try
        {
            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<VerificationCode>(
                    Builders<VerificationCode>.IndexKeys.Ascending(x => x.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expires_at_ttl" }),
                cancellationToken: cancellationToken);

            _indexEnsured = true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Не удалось создать TTL-индекс для кодов подтверждения; истёкшие коды пока не удаляются из базы");
        }
    }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string Hash(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
