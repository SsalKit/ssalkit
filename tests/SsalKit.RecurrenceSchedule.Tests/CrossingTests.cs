using System.Diagnostics;

namespace SsalKit.RecurrenceSchedule.Tests;

public sealed class CrossingTests
{
    private static readonly RecurrenceSchedule Daily0430 = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

    private static readonly DateTimeOffset Boundary = new(2026, 7, 25, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ABoundaryEqualToLastSeen_HasNotBeenCrossed()
    {
        Assert.False(Daily0430.HasCrossed(Boundary, Boundary.AddHours(1)));
        Assert.Equal(0, Daily0430.CountBoundaries(Boundary, Boundary.AddHours(1)));
    }

    [Fact]
    public void ABoundaryEqualToNow_HasJustBeenCrossed()
    {
        Assert.True(Daily0430.HasCrossed(Boundary.AddHours(-1), Boundary));
        Assert.Equal(1, Daily0430.CountBoundaries(Boundary.AddHours(-1), Boundary));
    }

    [Fact]
    public void CrossingIsDecidedToTheTick()
    {
        Assert.False(Daily0430.HasCrossed(Boundary.AddTicks(-1), Boundary.AddTicks(-1)));
        Assert.True(Daily0430.HasCrossed(Boundary.AddTicks(-1), Boundary));
        Assert.False(Daily0430.HasCrossed(Boundary, Boundary.AddTicks(1)));

        Assert.Equal(1, Daily0430.CountBoundaries(Boundary.AddTicks(-1), Boundary));
        Assert.Equal(0, Daily0430.CountBoundaries(Boundary, Boundary.AddTicks(1)));
    }

    [Fact]
    public void AnEmptyIntervalCrossesNothing()
    {
        Assert.False(Daily0430.HasCrossed(Boundary, Boundary));
        Assert.Equal(0, Daily0430.CountBoundaries(Boundary, Boundary));
    }

    [Fact]
    public void AReversedIntervalCrossesNothing()
    {
        var later = Boundary.AddDays(5);

        Assert.False(Daily0430.HasCrossed(later, Boundary));
        Assert.Equal(0, Daily0430.CountBoundaries(later, Boundary));

        // Even when boundaries lie between the two instants in the other direction.
        Assert.Equal(5, Daily0430.CountBoundaries(Boundary, later));
    }

    [Fact]
    public void HasCrossed_AgreesWithCountBoundaries()
    {
        var start = new DateTimeOffset(2026, 3, 6, 0, 0, 0, TimeSpan.FromHours(-5));
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);

        for (var minutes = 0; minutes <= 60 * 24 * 4; minutes += 37)
        {
            var now = start.AddMinutes(minutes);
            Assert.Equal(schedule.CountBoundaries(start, now) > 0, schedule.HasCrossed(start, now));
        }
    }

    [Fact]
    public void CountBoundaries_IsExactAcrossATenYearGap()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(0, 0));

        // 2020, 2024 and 2028 are leap years: 365 * 10 + 3 days, and one boundary per day.
        Assert.Equal(
            3653,
            schedule.CountBoundaries(
                new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Cross-checks the closed-form count against walking the schedule one boundary at a time with
    /// <see cref="RecurrenceSchedule.NextBoundary"/> — an independent path through the code that
    /// resolves each boundary individually. Run over a decade in zones with daylight saving, so
    /// twenty transitions are included.
    /// </summary>
    [Fact]
    public void CountBoundaries_MatchesWalkingTheScheduleBoundaryByBoundary()
    {
        RecurrenceSchedule[] schedules =
        [
            RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Daily(new TimeOnly(2, 15), TestTimeZones.LordHowe),
            RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0), TestTimeZones.NewYork),
        ];

        var from = new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5));
        var to = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5));

        foreach (var schedule in schedules)
        {
            var walked = 0;
            for (var cursor = schedule.NextBoundary(from); cursor <= to; cursor = schedule.NextBoundary(cursor))
            {
                walked++;
            }

            Assert.Equal(walked, schedule.CountBoundaries(from, to));
        }
    }

    /// <summary>
    /// A ten-year gap must cost the same as a one-day gap: the count comes from calendar
    /// arithmetic, not from stepping through 3,653 boundaries. The budget below is roughly a
    /// thousand times what the closed form needs and roughly a thousandth of what per-day
    /// iteration would.
    /// </summary>
    [Fact]
    public void CountBoundaries_DoesNotIterateOverTheGap()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(0, 0), TestTimeZones.NewYork);
        var from = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));
        var to = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        // Warm up the JIT and the time zone's adjustment-rule cache.
        Assert.Equal(3653, schedule.CountBoundaries(from, to));

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 1_000; i++)
        {
            schedule.CountBoundaries(from, to);
        }

        stopwatch.Stop();
        Assert.True(
            stopwatch.ElapsedMilliseconds < 500,
            $"1,000 ten-year counts took {stopwatch.ElapsedMilliseconds} ms, which suggests the gap is being walked.");
    }

    [Fact]
    public void CountBoundaries_SurvivesADecadeOfDaylightSavingTransitions()
    {
        // Twenty transitions in ten years must not add or drop a single daily boundary.
        var newYork = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var seoul = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.Seoul);

        Assert.Equal(
            3653,
            newYork.CountBoundaries(
                new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5))));
        Assert.Equal(
            3653,
            seoul.CountBoundaries(
                new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.FromHours(9)),
                new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(9))));
    }
}
