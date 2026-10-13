using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Tests.Fixtures;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AnemiaScanApi.Tests.Auth;

/// <summary>
/// Тест, которому нужна настоящая MongoDB. Адрес берётся из переменной
/// окружения <c>TEST_MONGO_URL</c>; без неё тест помечается пропущенным, а не
/// «зелёным» — пройденным он считается, только если действительно шёл.
///
///   docker run -d --rm -p 127.0.0.1:27019:27017 mongo:7
///   TEST_MONGO_URL=mongodb://127.0.0.1:27019 dotnet test
/// </summary>
public sealed class MongoFactAttribute : FactAttribute
{
    public const string UrlVariable = "TEST_MONGO_URL";

    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UrlVariable)))
            Skip = $"нужна MongoDB: задайте {UrlVariable}";
    }
}

/// <summary>
/// Хранилище кодов подтверждения на настоящей базе. Подменой коллекции это не
/// проверить: суть как раз в том, что делает сама Mongo — переживает ли код
/// «рестарт», заменяется ли старый код новым, стоит ли TTL-индекс.
/// </summary>
public sealed class VerificationCodeStoreMongoTests : IDisposable
{
    private const string Email = "user@example.com";

    private readonly MongoClient? _client;
    private readonly IMongoDatabase? _database;
    private readonly FixedTimeProvider _clock = new(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

    public VerificationCodeStoreMongoTests()
    {
        var url = Environment.GetEnvironmentVariable(MongoFactAttribute.UrlVariable);
        if (string.IsNullOrWhiteSpace(url)) return;

        _client = new MongoClient(url);
        // Своя база на каждый тест-класс: тесты ничего не делят и ничего не оставляют.
        _database = _client.GetDatabase("anemiascan_test_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose() => _client?.DropDatabase(_database!.DatabaseNamespace.DatabaseName);

    /// <summary>Новый экземпляр хранилища над той же базой — то же, что рестарт сервиса.</summary>
    private VerificationCodeStore NewStore()
        => new(_database!, NullLogger<VerificationCodeStore>.Instance, _clock);

    private IMongoCollection<BsonDocument> Raw
        => _database!.GetCollection<BsonDocument>(MongoCollection.VerificationCodes);

    [MongoFact]
    public async Task SavedCode_IsValid_WrongCodeIsMismatch_UnknownEmailIsMissing()
    {
        var store = NewStore();
        await store.SaveAsync(Email, "ABC123");

        (await store.CheckAsync(Email, "ABC123")).Should().Be(VerificationCodeCheck.Valid);
        (await store.CheckAsync(Email, "ZZZ999")).Should().Be(VerificationCodeCheck.Mismatch);
        (await store.CheckAsync(Email, "abc123")).Should().Be(VerificationCodeCheck.Mismatch, "код чувствителен к регистру");
        (await store.CheckAsync(Email, null)).Should().Be(VerificationCodeCheck.Mismatch);
        (await store.CheckAsync("other@example.com", "ABC123")).Should().Be(VerificationCodeCheck.Missing);
    }

    /// <summary>Ради этого хранилище и переехало из памяти процесса в базу.</summary>
    [MongoFact]
    public async Task Code_SurvivesServiceRestart()
    {
        await NewStore().SaveAsync(Email, "ABC123");

        var afterRestart = NewStore();

        (await afterRestart.CheckAsync(Email, "ABC123")).Should().Be(VerificationCodeCheck.Valid);
    }

    [MongoFact]
    public async Task CheckDoesNotConsumeCode()
    {
        var store = NewStore();
        await store.SaveAsync(Email, "ABC123");

        await store.CheckAsync(Email, "ABC123");
        await store.CheckAsync(Email, "WRONG1");

        // Сброс пароля идёт в два запроса с одним кодом: проверка, затем смена.
        (await store.CheckAsync(Email, "ABC123")).Should().Be(VerificationCodeCheck.Valid);
    }

    [MongoFact]
    public async Task NewCode_ReplacesPreviousOne()
    {
        var store = NewStore();
        await store.SaveAsync(Email, "FIRST1");
        await store.SaveAsync(Email, "SECOND");

        (await store.CheckAsync(Email, "FIRST1")).Should().Be(VerificationCodeCheck.Mismatch);
        (await store.CheckAsync(Email, "SECOND")).Should().Be(VerificationCodeCheck.Valid);
        (await Raw.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "на адрес — один документ");
    }

    [MongoFact]
    public async Task Code_ExpiresAfterLifetime_EvenBeforeMongoRemovesIt()
    {
        var store = NewStore();
        await store.SaveAsync(Email, "ABC123");

        _clock.Advance(VerificationCodeStore.Lifetime - TimeSpan.FromSeconds(1));
        (await store.CheckAsync(Email, "ABC123")).Should().Be(VerificationCodeCheck.Valid);

        _clock.Advance(TimeSpan.FromSeconds(2));
        (await store.CheckAsync(Email, "ABC123")).Should().Be(VerificationCodeCheck.Missing);

        // Документ физически ещё в базе: часы теста Mongo не видит, а её очистка
        // идёт раз в минуту. Истёкший код отсекает само условие запроса.
        (await Raw.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
    }

    [MongoFact]
    public async Task RemovedCode_IsMissing()
    {
        var store = NewStore();
        await store.SaveAsync(Email, "ABC123");

        await store.RemoveAsync(Email);

        (await store.CheckAsync(Email, "ABC123")).Should().Be(VerificationCodeCheck.Missing);
    }

    [MongoFact]
    public async Task Email_IsMatchedIgnoringCaseAndSpaces()
    {
        var store = NewStore();
        await store.SaveAsync("  User@Example.COM ", "ABC123");

        (await store.CheckAsync("user@example.com", "ABC123")).Should().Be(VerificationCodeCheck.Valid);

        await store.RemoveAsync("USER@example.com");
        (await store.CheckAsync("user@example.com", "ABC123")).Should().Be(VerificationCodeCheck.Missing);
    }

    [MongoFact]
    public async Task Code_IsNotStoredInPlainText()
    {
        await NewStore().SaveAsync(Email, "ABC123");

        var document = await Raw.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();

        document.ToJson().Should().NotContain("ABC123");
        document["_id"].AsString.Should().Be(Email);
        document["code_hash"].AsString.Should().HaveLength(64, "SHA-256 в hex");
    }

    [MongoFact]
    public async Task TtlIndex_IsCreatedOnExpiresAt()
    {
        await NewStore().SaveAsync(Email, "ABC123");

        var indexes = await (await Raw.Indexes.ListAsync()).ToListAsync();
        var ttl = indexes.Single(i => i["name"] == "expires_at_ttl");

        ttl["key"].AsBsonDocument.Names.Should().Equal("expires_at");
        ttl["expireAfterSeconds"].ToInt32().Should().Be(0, "документ удаляется в момент expires_at");
    }

    [MongoFact]
    public async Task ExpiredDocument_IsActuallyRemovedByMongo()
    {
        // Здесь часы настоящие: проверяем работу самого TTL-индекса. Срок ставим
        // в прошлое напрямую, чтобы не ждать пять минут.
        var store = new VerificationCodeStore(_database!, NullLogger<VerificationCodeStore>.Instance);
        await store.SaveAsync(Email, "ABC123");
        await Raw.UpdateOneAsync(FilterDefinition<BsonDocument>.Empty,
            Builders<BsonDocument>.Update.Set("expires_at", DateTime.UtcNow.AddMinutes(-1)));

        // Фоновая очистка Mongo просыпается раз в 60 секунд.
        var deadline = DateTime.UtcNow.AddSeconds(100);
        long left = 1;
        while (left > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            left = await Raw.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        }

        left.Should().Be(0, "TTL-индекс должен удалить истёкший код");
    }
}
