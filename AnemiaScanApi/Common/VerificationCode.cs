using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Код подтверждения, отправленный на почту. Один документ на адрес: новый код
/// заменяет предыдущий.
///
/// Не наследуется от <see cref="BaseMongoModel"/>: ключ здесь — сам адрес, а не
/// Guid. Так «последний код для адреса» — это чтение по _id, и двух кодов на
/// один адрес не может быть в принципе.
/// </summary>
public class VerificationCode
{
    /// <summary>Адрес в нижнем регистре без пробелов по краям.</summary>
    [BsonId] public string Email { get; set; } = null!;

    /// <summary>
    /// SHA-256 от кода, hex. Сам код в базу не пишется: он ещё и пропуск на вход
    /// (login-by-code), и ему незачем лежать открытым в дампах и логе запросов.
    /// </summary>
    [BsonElement("code_hash")] public string CodeHash { get; set; } = null!;

    /// <summary>
    /// Когда код перестаёт действовать. По этому полю стоит TTL-индекс, но
    /// проверяется оно и при чтении: фоновая очистка Mongo идёт раз в минуту.
    /// </summary>
    [BsonElement("expires_at")] public DateTime ExpiresAt { get; set; }

    [BsonElement("created_at")] public DateTime CreatedAt { get; set; }
}
