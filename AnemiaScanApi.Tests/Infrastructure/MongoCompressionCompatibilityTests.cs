using System.Reflection;
using System.Text;
using FluentAssertions;

namespace AnemiaScanApi.Tests.Infrastructure;

/// <summary>Exercise the driver's real compressors after security overrides of its dependencies.</summary>
public class MongoCompressionCompatibilityTests
{
    [Theory]
    [InlineData("ZlibCompressor")]
    [InlineData("SnappyCompressor")]
    public void UpdatedDependencies_PreserveMongoWireCompression(string compressorName)
    {
        var assembly = typeof(MongoDB.Driver.MongoClient).Assembly;
        var type = assembly.GetType($"MongoDB.Driver.Core.Compression.{compressorName}", throwOnError: true)!;
        var compressor = compressorName == "ZlibCompressor"
            ? Activator.CreateInstance(type, new object?[] { null })!
            : Activator.CreateInstance(type)!;
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("MongoDB compression compatibility; ", 100)));
        using var input = new MemoryStream(bytes);
        using var compressed = new MemoryStream();
        type.GetMethod("Compress")!.Invoke(compressor, [input, compressed]);
        compressed.Position = 0;
        using var output = new MemoryStream();
        type.GetMethod("Decompress")!.Invoke(compressor, [compressed, output]);
        output.ToArray().Should().Equal(bytes);
    }
}
