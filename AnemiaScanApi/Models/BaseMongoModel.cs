using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Models;

public class BaseMongoModel
{
    /// <summary>
    /// MongoDB ObjectId.
    /// </summary>
    [BsonId] public ObjectId Id { get; set; }    
}