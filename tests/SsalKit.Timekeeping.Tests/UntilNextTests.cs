namespace SsalKit.Timekeeping.Tests;

public sealed class UntilNextTests
{
    private static readonly TimeSpan Est = TimeSpan.FromHours(-5);
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);

    private static readonly RecurrenceSchedule Utc0430 = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

    [Fact]
    public void UntilNext_IsTheDistanceToTheNextBoundary()
    {
        var asOf = new DateTimeOffset(2026, 7, 25, 4, 15, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMinutes(15), Utc0430.UntilNext(asOf));
        Assert.Equal(Utc0430.NextBoundary(asOf) - asOf, Utc0430.UntilNext(asOf));
    }

    [Fact]
    public void UntilNext_AtABoundary_IsAWholeWindow_NotZero()
    {
        // NextBoundary is strict, so an instant that is itself a boundary has a full window ahead
        // of it rather than nothing.
        var boundary = new DateTimeOffset(2026, 7, 25, 4, 30, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromHours(24), Utc0430.UntilNext(boundary));
        Assert.True(Utc0430.UntilNext(boundary) > TimeSpan.Zero);
    }

    [Fact]
    public void UntilNext_IsStrictlyPositiveEverywhereInAWindow()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var cursor = new DateTimeOffset(2026, 3, 6, 0, 0, 0, Est);

        // Walks minute by minute across the spring-forward transition and a little beyond.
        for (var minutes = 0; minutes <= 60 * 24 * 5; minutes += 7)
        {
            var asOf = cursor.AddMinutes(minutes);
            var remaining = schedule.UntilNext(asOf);

            Assert.True(remaining > TimeSpan.Zero, $"UntilNext({asOf:O}) was {remaining}.");
            Assert.Equal(schedule.NextBoundary(asOf), asOf + remaining);
        }
    }

    [Fact]
    public void UntilNext_MeasuresElapsedTime_NotNominalCalendarTime()
    {
        // The window opened on 7 March at 02:30 EST closes at 03:00 EDT on the 8th: 23h30m of real
        // time, not 24 hours of wall clock.
        var spring = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        Assert.Equal(TimeSpan.FromHours(23.5), spring.UntilNext(new DateTimeOffset(2026, 3, 7, 2, 30, 0, Est)));

        // And the repeated hour on 1 November stretches the window to 25 hours.
        var autumn = RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork);
        Assert.Equal(TimeSpan.FromHours(25), autumn.UntilNext(new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt)));
    }

    [Fact]
    public void UntilNext_NeverExceedsTheCurrentWindowsDuration()
    {
        var schedule = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0), TestTimeZones.NewYork);
        var cursor = new DateTimeOffset(2026, 1, 15, 12, 0, 0, Est);

        for (var days = 0; days < 400; days += 3)
        {
            var asOf = cursor.AddDays(days);
            var window = schedule.CurrentWindow(asOf);

            Assert.Equal(window.End - asOf, schedule.UntilNext(asOf));
            Assert.True(schedule.UntilNext(asOf) <= window.Duration);
        }
    }

    [Fact]
    public void UntilNext_WorksForEveryCadence()
    {
        var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));
        var monthly = RecurrenceSchedule.Monthly(1, new TimeOnly(0, 0));

        // 2026-07-25 is a Saturday; the next Monday 09:00 is 2026-07-27.
        Assert.Equal(
            TimeSpan.FromHours(45),
            weekly.UntilNext(new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(
            TimeSpan.FromDays(7),
            monthly.UntilNext(new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero)));
    }
}
