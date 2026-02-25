using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

public class BaseMongoModel
{
    /// <summary>
    /// MongoDB Guid.
    /// </summary>
    [BsonId] public Guid Id { get; set; }
}