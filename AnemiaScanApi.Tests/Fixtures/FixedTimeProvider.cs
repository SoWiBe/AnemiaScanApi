namespace AnemiaScanApi.Tests.Fixtures;

/// <summary>
/// Controllable clock for payment TTL / auto-confirm timing. Hand-rolled rather than pulling in
/// Microsoft.Extensions.TimeProvider.Testing — the tests only need "now" and "advance".
/// </summary>
internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    private DateTimeOffset _now = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
