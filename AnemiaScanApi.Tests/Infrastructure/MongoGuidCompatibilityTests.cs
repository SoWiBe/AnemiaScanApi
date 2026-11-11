using AnemiaScanApi.Common;
using AnemiaScanApi.Infrastructure;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AnemiaScanApi.Tests.Infrastructure;

public class MongoGuidCompatibilityTests
{
    [Fact]
    public void ExistingGuidRepresentation_IsPreserved()
    {
        MongoSerialization.Configure();
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var document = new BaseMongoModel { Id = id }.ToBsonDocument();
        document["_id"].AsBsonBinaryData.SubType.Should().Be(BsonBinarySubType.UuidLegacy);
        document["_id"].AsBsonBinaryData.Bytes.Should().Equal(id.ToByteArray());
        BsonSerializer.Deserialize<BaseMongoModel>(document).Id.Should().Be(id);
    }

    [Fact]
    public void OwnershipFilters_UseTheExistingUuidByteOrder()
    {
        MongoSerialization.Configure();
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var registry = BsonSerializer.SerializerRegistry;
        var filter = Builders<CourseEnrollment>.Filter.Eq(e => e.UserId, id)
            .Render(new RenderArgs<CourseEnrollment>(registry.GetSerializer<CourseEnrollment>(), registry));
        var binary = filter.Elements.Single().Value.AsBsonBinaryData;
        binary.SubType.Should().Be(BsonBinarySubType.UuidLegacy);
        binary.Bytes.Should().Equal(id.ToByteArray());
    }
}
