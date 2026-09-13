using AnemiaScanApi.ML;
using FluentAssertions;

namespace AnemiaScanApi.Tests.ML;

public class SeverityBandsTests
{
    [Theory]
    [InlineData(3.1f, "Severe")]
    [InlineData(6.99f, "Severe")]
    [InlineData(7.0f, "Moderate")]
    [InlineData(9.99f, "Moderate")]
    [InlineData(10.0f, "Mild")]
    [InlineData(10.99f, "Mild")]
    [InlineData(11.0f, "Non-Anemic")]
    [InlineData(15.0f, "Non-Anemic")]
    public void Classify_MatchesWhoBands(float hb, string expected)
    {
        SeverityBands.Classify(hb).Should().Be(expected);
    }
}
