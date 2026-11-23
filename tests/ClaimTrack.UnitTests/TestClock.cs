using Microsoft.Extensions.Time.Testing;

namespace ClaimTrack.UnitTests;

// The one place unit tests define "today", so date rules give the same result whenever they run.
public static class TestClock
{
    public static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    public static FakeTimeProvider CreateTimeProvider() => new(Now);
}
