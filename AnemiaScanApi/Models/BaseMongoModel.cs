using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Models;

public class BaseMongoModel
{
    /// <summary>
    /// MongoDB Guid.
    /// </summary>
    [BsonId] public Guid Id { get; set; }
}