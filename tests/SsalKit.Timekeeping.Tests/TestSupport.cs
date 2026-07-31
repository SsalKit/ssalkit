namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// The time zones the daylight-saving contract is pinned against. IANA identifiers are used
/// deliberately: .NET 6+ resolves them on Windows as well, and the golden values below are derived
/// from the published transition rules of these zones, not from this library's own output.
/// </summary>
internal static class TestTimeZones
{
    /// <summary>
    /// One-hour DST. In 2026: forward on 8 March (02:00 EST becomes 03:00 EDT, so
    /// [02:00, 03:00) does not exist) and back on 1 November (02:00 EDT becomes 01:00 EST, so
    /// [01:00, 02:00) happens twice).
    /// </summary>
    public static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    /// <summary>Fixed +09:00 with no DST at all — the control case.</summary>
    public static readonly TimeZoneInfo Seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    /// <summary>
    /// Thirty-minute DST (+10:30 / +11:00), which catches rules that assume a one-hour shift. In
    /// 2026: forward on 4 October (02:00 becomes 02:30, so [02:00, 02:30) does not exist) and back
    /// on 5 April (02:00 becomes 01:30, so [01:30, 02:00) happens twice).
    /// </summary>
    public static readonly TimeZoneInfo LordHowe = TimeZoneInfo.FindSystemTimeZoneById("Australia/Lord_Howe");
}

internal static class AssertTime
{
    /// <summary>
    /// Asserts that two instants are equal <i>including their UTC offset</i>. Plain
    /// <see cref="Assert.Equal{T}(T, T)"/> would not: <see cref="DateTimeOffset"/> equality compares
    /// absolute instants, so it cannot tell a boundary reported as <c>01:30-04:00</c> from the same
    /// instant reported as <c>00:30-05:00</c> — which is exactly the distinction the schedule's
    /// "boundaries carry the zone's offset" contract is about.
    /// </summary>
    public static void Exact(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.True(
            expected.EqualsExact(actual),
            $"Expected {expected:O} (offset {expected.Offset}) but got {actual:O} (offset {actual.Offset}).");
}

/// <summary>
/// A minimal <see cref="TimeProvider"/> stuck at one instant. Deliberately hand-rolled rather than
/// pulled in from Microsoft.Extensions.TimeProvider.Testing: the extension methods under test only
/// ever call <see cref="TimeProvider.GetUtcNow"/>, so a package dependency would buy nothing.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
