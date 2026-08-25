using AnemiaScanApi.Common;

namespace AnemiaScanApi.Tests.Fixtures;

internal sealed class SasUserBuilder
{
    private readonly SasUser _user = new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        FullName = "Test User",
        HashPassword = "hash"
    };

    public SasUserBuilder WithId(Guid id)
    {
        _user.Id = id;
        return this;
    }

    public SasUserBuilder WithScan(AnemiaScan scan)
    {
        _user.AnemiaScans.Add(scan);
        return this;
    }

    public SasUserBuilder WithScans(params AnemiaScan[] scans)
    {
        _user.AnemiaScans.AddRange(scans);
        return this;
    }

    public SasUser Build() => _user;

    public static AnemiaScan AnemicScan(DateTime scanDate) => new()
    {
        Id = Guid.NewGuid(),
        AnalysisId = Guid.NewGuid().ToString(),
        IsAnemic = true,
        ScanDate = scanDate,
        Confidence = 0.9,
        UserId = Guid.NewGuid().ToString()
    };

    public static AnemiaScan HealthyScan(DateTime scanDate) => new()
    {
        Id = Guid.NewGuid(),
        AnalysisId = Guid.NewGuid().ToString(),
        IsAnemic = false,
        ScanDate = scanDate,
        Confidence = 0.9,
        UserId = Guid.NewGuid().ToString()
    };
}
