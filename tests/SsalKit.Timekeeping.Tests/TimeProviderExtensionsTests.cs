namespace SsalKit.Timekeeping.Tests;

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
        Assert.Equal(Schedule.PreviousBoundary(Now), Schedule.PreviousBoundary(Clock));
        Assert.Equal(Schedule.UntilNext(Now), Schedule.UntilNext(Clock));
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
            Schedule.PreviousBoundary(Clock));
        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 24, 4, 30, 0, TimeSpan.FromHours(9)),
            Schedule.CurrentWindow(Clock).Start);
        Assert.Equal(TimeSpan.FromMinutes(15), Schedule.UntilNext(Clock));
        Assert.True(Schedule.HasCrossed(Now.AddDays(-2), Clock));
        Assert.Equal(2, Schedule.CountBoundaries(Now.AddDays(-2), Clock));
    }

    [Fact]
    public void TheOverloadsRejectANullSchedule()
    {
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).CurrentWindow(Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).NextBoundary(Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).PreviousBoundary(Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).UntilNext(Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).HasCrossed(Now, Clock));
        Assert.Throws<ArgumentNullException>(() => ((RecurrenceSchedule)null!).CountBoundaries(Now, Clock));
    }

    [Fact]
    public void TheOverloadsRejectANullTimeProvider()
    {
        Assert.Throws<ArgumentNullException>(() => Schedule.CurrentWindow(null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.NextBoundary(null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.PreviousBoundary(null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.UntilNext(null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.HasCrossed(Now, null!));
        Assert.Throws<ArgumentNullException>(() => Schedule.CountBoundaries(Now, null!));
    }

    /// <summary>
    /// Each overload must read the clock exactly once. <c>UntilNext</c> is the one where it
    /// matters: an implementation that read <c>GetUtcNow()</c> for the boundary and again for the
    /// subtraction would return a torn, slightly-too-large duration against a moving clock.
    /// </summary>
    [Fact]
    public void EachOverloadReadsTheClockExactlyOnce()
    {
        var counting = new CountingTimeProvider(Now);

        Schedule.UntilNext(counting);
        Assert.Equal(1, counting.Reads);

        Schedule.PreviousBoundary(counting);
        Schedule.NextBoundary(counting);
        Schedule.CurrentWindow(counting);
        Schedule.HasCrossed(Now.AddDays(-2), counting);
        Schedule.CountBoundaries(Now.AddDays(-2), counting);
        Assert.Equal(6, counting.Reads);
    }

    private sealed class CountingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return utcNow;
        }
    }
}
