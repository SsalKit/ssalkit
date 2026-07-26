namespace SsalKit.RecurrenceSchedule.Tests;

public sealed class TimeProviderExtensionsTests
{
    private static readonly RecurrenceSchedule Schedule =
        RecurrenceSchedule.Daily(new TimeOnly(4, 30), TestTimeZones.Seoul);

    private static readonly DateTimeOffset Now = new(2026, 7, 25, 4, 15, 0, TimeSpan.FromHours(9));

    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    [Fact]
    public void TheOverloadsForwardTheProvidersInstant()
    {
        Assert.Equal(Schedule.CurrentWindow(Now), Schedule.CurrentWindow(Clock));
        Assert.Equal(Schedule.NextBoundary(Now), Schedule.NextBoundary(Clock));
        Assert.Equal(Schedule.HasCrossed(Now.AddDays(-2), Now), Schedule.HasCrossed(Now.AddDays(-2), Clock));
        Assert.Equal(Schedule.CountBoundaries(Now.AddDays(-2), Now), Schedule.CountBoundaries(Now.AddDays(-2), Clock));
    }

    [Fact]
    public void TheOverloadsPreserveTheScheduleTimeZonesOffset()
    {
        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 25, 4, 30, 0, TimeSpan.FromHours(9)),
            Schedule.NextBoundary(Clock));
        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 24, 4, 30, 0, TimeSpan.FromHours(9)),
            Schedule.CurrentWindow(Clock).Start);
        Assert.True(Schedule.HasCrossed(Now.AddDays(-2), Clock));
        Assert.Equal(2, Schedule.CountBoundaries(Now.AddDays(-2), Clock));
    }

    [Fact]
    public void TheOverloadsRejectANullSchedule()
    {
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).CurrentWindow(Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).NextBoundary(Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).HasCrossed(Now, Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).CountBoundaries(Now, Clock));
    }

    [Fact]
    public void TheOverloadsRejectANullTimeProvider()
    {
        Assert.Throws<ArgumentNullException>(() => Schedule.CurrentWindow(null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.NextBoundary(null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.HasCrossed(Now, null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.CountBoundaries(Now, null!));
    }
}
