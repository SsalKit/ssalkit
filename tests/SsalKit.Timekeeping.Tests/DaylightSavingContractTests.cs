namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// Golden cases for the daylight-saving contract. Every expected value here comes from the
/// published transition rules of the zone (US: second Sunday in March / first Sunday in November at
/// 02:00 local, one hour; Lord Howe: first Sunday in October / first Sunday in April at 02:00
/// local, thirty minutes) combined with the three resolution rules, so these tests pin the contract
/// rather than merely describing the implementation.
/// </summary>
public sealed class DaylightSavingContractTests
{
    private static readonly TimeSpan Est = TimeSpan.FromHours(-5);
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);
    private static readonly TimeSpan LordHoweStandard = new(10, 30, 0);
    private static readonly TimeSpan LordHoweDaylight = TimeSpan.FromHours(11);

    // ---------------------------------------------------------------------------------------
    // Rule 1 — a scheduled wall-clock time that does not exist moves to the first valid instant
    // after the gap, and the boundary is never lost.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void SkippedWallClockTime_MovesToTheFirstInstantAfterTheGap()
    {
        // 2026-03-08 in New York: 02:00 EST becomes 03:00 EDT, so 02:30 never happens.
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);

        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt),
            schedule.NextBoundary(new DateTimeOffset(2026, 3, 8, 1, 59, 59, Est)));

        // The neighbouring days are untouched, each at its own offset.
        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 7, 2, 30, 0, Est),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, Est)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 9, 2, 30, 0, Edt),
            schedule.NextBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));
    }

    [Fact]
    public void SkippedWallClockTime_LandsOnTheTransition_NotTheScheduledTimeShiftedByTheGap()
    {
        // 2026-10-04 on Lord Howe: 02:00 becomes 02:30, a thirty-minute gap. A 02:15 schedule must
        // resolve to 02:30 (the transition), not to 02:45 (02:15 pushed forward by the gap).
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 15), TestTimeZones.LordHowe);

        AssertTime.Exact(
            new DateTimeOffset(2026, 10, 4, 2, 30, 0, LordHoweDaylight),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 10, 4, 12, 0, 0, LordHoweDaylight)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 10, 3, 2, 15, 0, LordHoweStandard),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 10, 4, 1, 0, 0, LordHoweStandard)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 10, 5, 2, 15, 0, LordHoweDaylight),
            schedule.NextBoundary(new DateTimeOffset(2026, 10, 4, 12, 0, 0, LordHoweDaylight)));
    }

    [Fact]
    public void SkippedWallClockTime_AtTheVeryStartOfTheGap_StillResolvesToTheTransition()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 0), TestTimeZones.NewYork);

        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));
    }

    [Fact]
    public void SkippedWallClockTime_OfASecondsPreciseSchedule_StillLandsExactlyOnTheTransition()
    {
        // 02:30:15 falls inside the gap just as 02:30 does, but its seconds put the transition
        // strictly between two whole minutes of the schedule's own reckoning — so resolving it
        // exercises the sub-minute half of the search rather than landing on a round number.
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30, 15), TestTimeZones.NewYork);

        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));

        // The neighbouring days keep their seconds.
        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 7, 2, 30, 15, Est),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, Est)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 9, 2, 30, 15, Edt),
            schedule.NextBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));
    }

    [Fact]
    public void SkippedWallClockTime_DoesNotCostTheDayItsBoundary()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);

        // March 2026 has 31 days, so 31 boundaries — the transition day included.
        Assert.Equal(
            31,
            schedule.CountBoundaries(
                new DateTimeOffset(2026, 3, 1, 0, 0, 0, Est),
                new DateTimeOffset(2026, 4, 1, 0, 0, 0, Edt)));

        // Two days spanning the transition still contain exactly two boundaries.
        Assert.Equal(
            2,
            schedule.CountBoundaries(
                new DateTimeOffset(2026, 3, 7, 12, 0, 0, Est),
                new DateTimeOffset(2026, 3, 9, 12, 0, 0, Edt)));
    }

    [Fact]
    public void SkippedWallClockTime_ShortensTheWindowsAroundIt_ButLosesNoTime()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);

        var before = schedule.CurrentWindow(new DateTimeOffset(2026, 3, 8, 0, 0, 0, Est));
        var after = schedule.CurrentWindow(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));

        // 02:30 EST on the 7th to 03:00 EDT on the 8th is 23h30m; 03:00 EDT on the 8th to
        // 02:30 EDT on the 9th is another 23h30m. Together 47 hours: two civil days minus the
        // hour the clocks skipped.
        Assert.Equal(TimeSpan.FromHours(23.5), before.Duration);
        Assert.Equal(TimeSpan.FromHours(23.5), after.Duration);
        Assert.Equal(before.End, after.Start);
    }

    // ---------------------------------------------------------------------------------------
    // Rule 2 — a scheduled wall-clock time that happens twice resolves to the first occurrence.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void RepeatedWallClockTime_ResolvesToTheFirstOccurrence()
    {
        // 2026-11-01 in New York: 02:00 EDT becomes 01:00 EST, so 01:30 happens twice —
        // at 05:30Z (EDT) and again at 06:30Z (EST). The contract picks the first.
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork);
        var firstOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt);

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), firstOccurrence);
        AssertTime.Exact(
            firstOccurrence,
            schedule.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est)));
        AssertTime.Exact(
            firstOccurrence,
            schedule.NextBoundary(new DateTimeOffset(2026, 11, 1, 0, 30, 0, Edt)));
    }

    [Fact]
    public void RepeatedWallClockTime_DoesNotFireTwiceInOneDay()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork);
        var firstOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt);
        var secondOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Est);

        // Nothing fires during the repeated hour...
        Assert.Equal(0, schedule.CountBoundaries(firstOccurrence, secondOccurrence));
        Assert.False(schedule.HasCrossed(firstOccurrence, secondOccurrence));

        // ...and the second 01:30 is still inside the window the first one opened.
        AssertTime.Exact(firstOccurrence, schedule.PreviousBoundary(secondOccurrence));
        Assert.True(schedule.CurrentWindow(secondOccurrence).Contains(secondOccurrence));

        // Two days spanning the transition contain exactly two boundaries, not three.
        Assert.Equal(
            2,
            schedule.CountBoundaries(
                new DateTimeOffset(2026, 10, 31, 12, 0, 0, Edt),
                new DateTimeOffset(2026, 11, 2, 12, 0, 0, Est)));
        Assert.Equal(
            30,
            schedule.CountBoundaries(
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, Edt),
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, Est)));
    }

    [Fact]
    public void RepeatedWallClockTime_IsCrossedOnceOnTheFirstPass()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork);

        Assert.True(schedule.HasCrossed(
            new DateTimeOffset(2026, 11, 1, 0, 59, 0, Edt),
            new DateTimeOffset(2026, 11, 1, 1, 0, 0, Est)));
        Assert.Equal(
            TimeSpan.FromHours(25),
            schedule.CurrentWindow(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est)).Duration);
    }

    [Fact]
    public void RepeatedWallClockTime_UsesThePreTransitionOffset_ForAThirtyMinuteShiftToo()
    {
        // 2026-04-05 on Lord Howe: 02:00 (+11:00) becomes 01:30 (+10:30), so [01:30, 02:00)
        // happens twice. The first occurrence is the one at the larger, pre-transition offset.
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(1, 45), TestTimeZones.LordHowe);

        AssertTime.Exact(
            new DateTimeOffset(2026, 4, 5, 1, 45, 0, LordHoweDaylight),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 4, 5, 12, 0, 0, LordHoweStandard)));
        Assert.Equal(
            2,
            schedule.CountBoundaries(
                new DateTimeOffset(2026, 4, 4, 12, 0, 0, LordHoweDaylight),
                new DateTimeOffset(2026, 4, 6, 12, 0, 0, LordHoweStandard)));
    }

    // ---------------------------------------------------------------------------------------
    // Rule 3 — every other wall-clock time keeps its local time year-round.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void OrdinaryWallClockTime_KeepsItsLocalTimeAcrossTheSeasons()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(9, 0), TestTimeZones.NewYork);

        AssertTime.Exact(
            new DateTimeOffset(2026, 1, 15, 9, 0, 0, Est),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 1, 15, 12, 0, 0, Est)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 15, 9, 0, 0, Edt),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 7, 15, 12, 0, 0, Edt)));

        // The whole year still has exactly 365 boundaries.
        Assert.Equal(
            365,
            schedule.CountBoundaries(
                new DateTimeOffset(2026, 1, 1, 9, 0, 0, Est),
                new DateTimeOffset(2027, 1, 1, 9, 0, 0, Est)));
    }

    [Fact]
    public void AZoneWithoutDaylightSaving_IsUnaffectedOnTheSameDates()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.Seoul);
        var seoul = TimeSpan.FromHours(9);

        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 2, 30, 0, seoul),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, seoul)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 11, 1, 2, 30, 0, seoul),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, seoul)));
        Assert.Equal(
            TimeSpan.FromHours(24),
            schedule.CurrentWindow(new DateTimeOffset(2026, 3, 8, 12, 0, 0, seoul)).Duration);
    }

    // ---------------------------------------------------------------------------------------
    // The rules are cadence-independent.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheRulesApplyToWeeklySchedulesToo()
    {
        // 8 March 2026 and 1 November 2026 are both Sundays.
        var skipped = RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30), TestTimeZones.NewYork);
        var repeated = RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(1, 30), TestTimeZones.NewYork);

        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt),
            skipped.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt),
            repeated.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est)));
        Assert.Equal(
            1,
            skipped.CountBoundaries(
                new DateTimeOffset(2026, 3, 7, 12, 0, 0, Est),
                new DateTimeOffset(2026, 3, 9, 12, 0, 0, Edt)));
    }

    [Fact]
    public void TheRulesApplyToMonthlySchedulesToo()
    {
        var skipped = RecurrenceSchedule.Monthly(8, new TimeOnly(2, 30), TestTimeZones.NewYork);
        var repeated = RecurrenceSchedule.Monthly(1, new TimeOnly(1, 30), TestTimeZones.NewYork);

        AssertTime.Exact(
            new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt),
            skipped.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt),
            repeated.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est)));
        Assert.Equal(
            12,
            skipped.CountBoundaries(
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, Est),
                new DateTimeOffset(2027, 1, 1, 0, 0, 0, Est)));
    }
}
