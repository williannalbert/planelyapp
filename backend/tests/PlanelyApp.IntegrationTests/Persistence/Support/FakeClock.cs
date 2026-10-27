namespace PlanelyApp.IntegrationTests.Persistence.Support;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}