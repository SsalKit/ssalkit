namespace SsalKit.Timekeeping.Tests;

public sealed class WindowAtTests
{
    private static readonly TimeSpan Est = TimeSpan.FromHours(-5);
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);

    private static readonly RecurrenceSchedule Utc0430 = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(2026, 7, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void OffsetZero_IsExactlyCurrentWindow()
    {
        RecurrenceSchedule[] schedules =
        [
            Utc0430,
            RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0), TestTimeZones.LordHowe),
        ];

        var origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        foreach (var schedule in schedules)
        {
            for (var hours = 0; hours < 24 * 400; hours += 37)
            {
                var asOf = origin.AddHours(hours);
                var current = schedule.CurrentWindow(asOf);
                var at0 = schedule.WindowAt(asOf, 0);

                Assert.Equal(current, at0);
                AssertTime.Exact(current.Start, at0.Start);
                AssertTime.Exact(current.End, at0.End);
            }
        }
    }

    [Fact]
    public void ANegativeOffsetIsThePastWindow_APositiveOneTheFuture()
    {
        var asOf = Utc(25, 12);

        var yesterday = Utc0430.WindowAt(asOf, -1);
        var today = Utc0430.WindowAt(asOf, 0);
        var tomorrow = Utc0430.WindowAt(asOf, 1);

        AssertTime.Exact(Utc(24, 4, 30), yesterday.Start);
        AssertTime.Exact(Utc(25, 4, 30), yesterday.End);
        AssertTime.Exact(Utc(25, 4, 30), today.Start);
        AssertTime.Exact(Utc(26, 4, 30), today.End);
        AssertTime.Exact(Utc(26, 4, 30), tomorrow.Start);
        AssertTime.Exact(Utc(27, 4, 30), tomorrow.End);
    }

    [Fact]
    public void ConsecutiveOffsetsTileTheTimelineWithoutGapOrOverlap()
    {
        RecurrenceSchedule[] schedules =
        [
            RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Daily(new TimeOnly(2, 15), TestTimeZones.LordHowe),
            RecurrenceSchedule.Weekly(DayOfWeek.Wednesday, new TimeOnly(9, 0), TestTimeZones.NewYork),
            RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0), TestTimeZones.NewYork),
        ];

        var asOf = new DateTimeOffset(2026, 6, 15, 12, 0, 0, Edt);

        foreach (var schedule in schedules)
        {
            // Wide enough that the daily schedules sweep through both 2026 transitions in each
            // direction, and the monthly one through several years.
            for (var offset = -400; offset < 400; offset++)
            {
                var window = schedule.WindowAt(asOf, offset);
                var next = schedule.WindowAt(asOf, offset + 1);

                Assert.Equal(window.End, next.Start);
                Assert.False(window.Overlaps(next));
                Assert.True(window.Duration > TimeSpan.Zero);
            }
        }
    }

    [Fact]
    public void SteppingByOneOffsetIsTheSameAsRelocatingToTheNeighbouringWindow()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var asOf = new DateTimeOffset(2026, 3, 10, 12, 0, 0, Edt);

        for (var offset = -20; offset <= 20; offset++)
        {
            var window = schedule.WindowAt(asOf, offset);

            // The window is the one its own start instant belongs to...
            Assert.Equal(window, schedule.CurrentWindow(window.Start));

            // ...and the neighbours agree from either side.
            Assert.Equal(schedule.WindowAt(asOf, offset + 1), schedule.WindowAt(window.Start, 1));
            Assert.Equal(schedule.WindowAt(asOf, offset - 1), schedule.WindowAt(window.Start, -1));
        }
    }

    [Fact]
    public void TheOffsetCountsWindows_NotCalendarDays_AcrossADaylightSavingTransition()
    {
        // 2026-03-08 in New York: the 02:30 boundary moves to the 03:00 transition, so the two
        // windows either side of it are 23h30m rather than 24h — but there is still exactly one
        // window per calendar day, and WindowAt(-1) is still "yesterday".
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var asOf = new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt);

        var today = schedule.WindowAt(asOf, 0);
        var yesterday = schedule.WindowAt(asOf, -1);

        AssertTime.Exact(new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt), today.Start);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 9, 2, 30, 0, Edt), today.End);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 7, 2, 30, 0, Est), yesterday.Start);
        Assert.Equal(TimeSpan.FromHours(23.5), yesterday.Duration);
        Assert.Equal(TimeSpan.FromHours(23.5), today.Duration);

        // The repeated hour on 1 November stretches its window the other way.
        var autumn = RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork);
        Assert.Equal(
            TimeSpan.FromHours(25),
            autumn.WindowAt(new DateTimeOffset(2026, 11, 2, 12, 0, 0, Est), -1).Duration);
    }

    [Fact]
    public void TheOffsetAgreesWithCountBoundaries()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var asOf = new DateTimeOffset(2026, 6, 15, 12, 0, 0, Edt);
        var current = schedule.CurrentWindow(asOf);

        for (var offset = 1; offset <= 200; offset++)
        {
            var future = schedule.WindowAt(asOf, offset);
            var past = schedule.WindowAt(asOf, -offset);

            Assert.Equal(offset, schedule.CountBoundaries(current.Start, future.Start));
            Assert.Equal(offset, schedule.CountBoundaries(past.Start, current.Start));
        }
    }

    [Fact]
    public void TheOffsetWorksForWeeklyAndMonthlyCadences()
    {
        var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));
        var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

        // 2026-07-25 is a Saturday, so the current week opened on Monday 2026-07-20.
        var lastWeek = weekly.WindowAt(Utc(25, 12), -1);
        AssertTime.Exact(new DateTimeOffset(2026, 7, 13, 9, 0, 0, TimeSpan.Zero), lastWeek.Start);
        AssertTime.Exact(new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero), lastWeek.End);

        // A monthly schedule anchored to day 31 clamps in short months, and the offset still counts
        // months rather than 31-day steps. As of 15 April the current window opened on 31 March, so
        // the one before it is the window February's clamped boundary opened.
        var asOf = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), monthly.WindowAt(asOf, 0).Start);

        var february = monthly.WindowAt(asOf, -1);
        AssertTime.Exact(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), february.Start);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), february.End);
    }

    // ---------------------------------------------------------------------------------------
    // Extreme offsets are rejected rather than wrapped around.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnOffsetThatOverflowsTheOccurrenceArithmetic_Throws()
    {
        // A weekly schedule multiplies the offset by seven, so int.MinValue leaves the range of
        // int before it ever reaches a calendar. Silently wrapping would hand back a window from
        // some unrelated century.
        var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));

        var tooFarBack = Assert.Throws<ArgumentOutOfRangeException>(
            () => weekly.WindowAt(Utc(25, 12), int.MinValue));
        var tooFarForward = Assert.Throws<ArgumentOutOfRangeException>(
            () => weekly.WindowAt(Utc(25, 12), int.MaxValue));

        Assert.Equal("offset", tooFarBack.ParamName);
        Assert.Equal("offset", tooFarForward.ParamName);
    }

    [Fact]
    public void AnOffsetPastTheRepresentableCalendarRange_Throws()
    {
        // A daily schedule stays inside int here, so the rejection comes from the date arithmetic
        // instead — the same ArgumentOutOfRangeException the range contract already documents.
        Assert.Throws<ArgumentOutOfRangeException>(() => Utc0430.WindowAt(Utc(25, 12), int.MinValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Utc0430.WindowAt(Utc(25, 12), -1_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => Utc0430.WindowAt(Utc(25, 12), 3_000_000));
    }

    [Fact]
    public void TheWholeRepresentableRangeIsReachable()
    {
        // Day number 0 is 0001-01-01 and the last window of the calendar opens on 9999-12-31, so
        // both ends are reachable by offset without tripping the guard.
        var midnight = RecurrenceSchedule.Daily(new TimeOnly(0, 0));
        var asOf = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(asOf.UtcDateTime).DayNumber;

        AssertTime.Exact(
            new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero),
            midnight.WindowAt(asOf, -today).Start);
        AssertTime.Exact(
            new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero),
            midnight.WindowAt(asOf, DateOnly.MaxValue.DayNumber - today - 1).End);
    }
}
