using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// Факт согласия на обработку персональных данных (P0 №13 в docs/plans/MVP_PLAN.md).
/// Галка на фронте, которую сервер не записал, юридически не существует —
/// поэтому версия политики и момент принятия хранятся в документе пользователя.
/// </summary>
public class UserConsent
{
    /// <summary>Версия политики, под которой подписался пользователь.</summary>
    [BsonElement("policy_version")] public string PolicyVersion { get; set; } = null!;

    /// <summary>Момент принятия, UTC.</summary>
    [BsonElement("accepted_at")] public DateTime AcceptedAt { get; set; }

    /// <summary>
    /// Факт принятия. Отдельное поле, а не «раз запись есть — значит принято»:
    /// при отзыве согласия запись остаётся, но с <c>false</c>.
    /// </summary>
    [BsonElement("accepted")] public bool Accepted { get; set; }
}
