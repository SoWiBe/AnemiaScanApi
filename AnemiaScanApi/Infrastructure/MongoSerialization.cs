using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AnemiaScanApi.Infrastructure;

/// <summary>Keep existing MongoDB 2.x UUID byte order when using the 3.x driver.</summary>
public static class MongoSerialization
{
    static MongoSerialization()
        => BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));

    // An explicit static constructor guarantees registration once, before any entity is mapped.
    public static void Configure() { }
}
