namespace SsalKit.RecurrenceSchedule.Tests;

public sealed class DailyBoundaryTests
{
    private static readonly RecurrenceSchedule Utc0430 = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(2026, 7, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void PreviousBoundary_IsTheLatestBoundaryAtOrBeforeTheInstant()
    {
        AssertTime.Exact(Utc(24, 4, 30), Utc0430.PreviousBoundary(Utc(25, 4, 15)));
        AssertTime.Exact(Utc(25, 4, 30), Utc0430.PreviousBoundary(Utc(25, 4, 30)));
        AssertTime.Exact(Utc(25, 4, 30), Utc0430.PreviousBoundary(Utc(25, 23, 59)));
    }

    [Fact]
    public void NextBoundary_IsTheEarliestBoundaryStrictlyAfterTheInstant()
    {
        AssertTime.Exact(Utc(25, 4, 30), Utc0430.NextBoundary(Utc(25, 4, 15)));
        AssertTime.Exact(Utc(26, 4, 30), Utc0430.NextBoundary(Utc(25, 4, 30)));
        AssertTime.Exact(Utc(25, 4, 30), Utc0430.NextBoundary(Utc(25, 4, 30).AddTicks(-1)));
    }

    [Fact]
    public void CurrentWindow_SpansPreviousToNextBoundary()
    {
        var window = Utc0430.CurrentWindow(Utc(25, 4, 15));

        AssertTime.Exact(Utc(24, 4, 30), window.Start);
        AssertTime.Exact(Utc(25, 4, 30), window.End);
        Assert.Equal(TimeSpan.FromHours(24), window.Duration);
        Assert.True(window.Contains(Utc(25, 4, 15)));
    }

    [Fact]
    public void CurrentWindow_TreatsABoundaryAsOpeningItsOwnWindow()
    {
        var boundary = Utc(25, 4, 30);
        var window = Utc0430.CurrentWindow(boundary);

        AssertTime.Exact(boundary, window.Start);
        Assert.True(window.Contains(boundary));
        Assert.False(Utc0430.CurrentWindow(boundary.AddTicks(-1)).Contains(boundary));
    }

    [Fact]
    public void ConsecutiveWindows_TileTheTimelineWithoutGapOrOverlap()
    {
        var previous = Utc0430.CurrentWindow(Utc(24, 12));
        var current = Utc0430.CurrentWindow(Utc(25, 12));

        Assert.Equal(previous.End, current.Start);
        Assert.False(previous.Overlaps(current));
    }

    /// <summary>
    /// Regression against the prototype this library replaces, which decided "has the reset
    /// happened yet" with <c>from.Hour &gt;= resetHour</c> — so 04:15 counted as past an 04:30
    /// reset because 4 &gt;= 4, and every sub-hour part of the schedule was silently discarded.
    /// The API only ever compares instants, so the whole bug class is unrepresentable.
    /// </summary>
    [Fact]
    public void MinuteResolution_IsHonoured_NotJustTheHourField()
    {
        Assert.False(Utc0430.HasCrossed(Utc(25, 4, 0), Utc(25, 4, 15)));
        Assert.False(Utc0430.HasCrossed(Utc(25, 4, 0), Utc(25, 4, 30).AddTicks(-1)));
        Assert.True(Utc0430.HasCrossed(Utc(25, 4, 0), Utc(25, 4, 30)));

        Assert.Equal(0, Utc0430.CountBoundaries(Utc(25, 4, 0), Utc(25, 4, 15)));
        Assert.Equal(1, Utc0430.CountBoundaries(Utc(25, 4, 0), Utc(25, 4, 30)));

        // ...and the window at 04:15 is still yesterday's, not today's.
        AssertTime.Exact(Utc(24, 4, 30), Utc0430.CurrentWindow(Utc(25, 4, 15)).Start);
    }

    [Fact]
    public void Midnight_IsAnOrdinaryBoundary()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(0, 0));

        AssertTime.Exact(Utc(25, 0, 0), schedule.PreviousBoundary(Utc(25, 0, 0)));
        AssertTime.Exact(Utc(26, 0, 0), schedule.NextBoundary(Utc(25, 0, 0)));
        AssertTime.Exact(Utc(24, 0, 0), schedule.PreviousBoundary(Utc(25, 0, 0).AddTicks(-1)));
    }

    [Fact]
    public void EndOfDay_IsAnOrdinaryBoundary()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(23, 59, 59));

        AssertTime.Exact(new DateTimeOffset(2026, 7, 24, 23, 59, 59, TimeSpan.Zero), schedule.PreviousBoundary(Utc(25, 12)));
        AssertTime.Exact(new DateTimeOffset(2026, 7, 25, 23, 59, 59, TimeSpan.Zero), schedule.NextBoundary(Utc(25, 12)));
    }

    [Fact]
    public void AFixedOffsetZone_KeepsItsOffsetOnEveryBoundary()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(4, 30), TestTimeZones.Seoul);
        var seoul = TimeSpan.FromHours(9);

        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 24, 4, 30, 0, seoul),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 7, 25, 4, 15, 0, seoul)));

        // Seoul has had no DST since 1988, so the offset is the same in January and July.
        AssertTime.Exact(
            new DateTimeOffset(2026, 1, 15, 4, 30, 0, seoul),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 1, 15, 12, 0, 0, seoul)));
        Assert.Equal(365, schedule.CountBoundaries(
            new DateTimeOffset(2026, 1, 1, 4, 30, 0, seoul),
            new DateTimeOffset(2027, 1, 1, 4, 30, 0, seoul)));
    }

    [Fact]
    public void TheAsOfInstantMayBeExpressedInAnyOffset()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(4, 30), TestTimeZones.Seoul);

        // 2026-07-25T04:15+09:00 is 2026-07-24T19:15Z; both spellings must locate the same window.
        var fromSeoul = schedule.CurrentWindow(new DateTimeOffset(2026, 7, 25, 4, 15, 0, TimeSpan.FromHours(9)));
        var fromUtc = schedule.CurrentWindow(new DateTimeOffset(2026, 7, 24, 19, 15, 0, TimeSpan.Zero));

        Assert.Equal(fromSeoul, fromUtc);
        AssertTime.Exact(new DateTimeOffset(2026, 7, 24, 4, 30, 0, TimeSpan.FromHours(9)), fromUtc.Start);
    }
}
