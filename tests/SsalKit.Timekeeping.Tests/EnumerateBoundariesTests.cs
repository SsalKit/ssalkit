namespace SsalKit.Timekeeping.Tests;

public sealed class EnumerateBoundariesTests
{
    private static readonly TimeSpan Est = TimeSpan.FromHours(-5);
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);
    private static readonly TimeSpan LordHoweStandard = new(10, 30, 0);
    private static readonly TimeSpan LordHoweDaylight = TimeSpan.FromHours(11);

    private static readonly RecurrenceSchedule Utc0430 = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(2026, 7, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void TheBoundariesComeBackInAscendingOrder()
    {
        DateTimeOffset[] expected = [Utc(25, 4, 30), Utc(26, 4, 30), Utc(27, 4, 30), Utc(28, 4, 30)];

        Assert.Equal(expected, Utc0430.EnumerateBoundaries(Utc(24, 12), Utc(28, 12)).ToArray());
    }

    [Fact]
    public void TheIntervalIsHalfOpen_ExclusiveAtFromAndInclusiveAtTo()
    {
        var first = Utc(25, 4, 30);
        var second = Utc(26, 4, 30);

        // A boundary exactly at `from` is not yielded; one exactly at `to` is.
        DateTimeOffset[] onlySecond = [second];
        Assert.Equal(onlySecond, Utc0430.EnumerateBoundaries(first, second).ToArray());

        // One tick either side moves each endpoint in or out.
        DateTimeOffset[] both = [first, second];
        Assert.Equal(both, Utc0430.EnumerateBoundaries(first.AddTicks(-1), second).ToArray());
        Assert.Empty(Utc0430.EnumerateBoundaries(first, second.AddTicks(-1)));
    }

    [Fact]
    public void AnEmptyOrReversedIntervalYieldsNothing()
    {
        Assert.Empty(Utc0430.EnumerateBoundaries(Utc(25, 12), Utc(25, 12)));
        Assert.Empty(Utc0430.EnumerateBoundaries(Utc(28, 12), Utc(24, 12)));

        // Reversed even when the interval read the other way round is full of boundaries.
        Assert.Equal(4, Utc0430.CountBoundaries(Utc(24, 12), Utc(28, 12)));
    }

    [Fact]
    public void AnIntervalInsideOneWindowYieldsNothing()
    {
        Assert.Empty(Utc0430.EnumerateBoundaries(Utc(25, 5), Utc(25, 23)));
    }

    [Fact]
    public void TheBoundariesCarryTheScheduleTimeZonesOffset()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(9, 0), TestTimeZones.NewYork);

        var winter = schedule.EnumerateBoundaries(
            new DateTimeOffset(2026, 1, 14, 12, 0, 0, Est),
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, Est)).Single();
        var summer = schedule.EnumerateBoundaries(
            new DateTimeOffset(2026, 7, 14, 12, 0, 0, Edt),
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, Edt)).Single();

        AssertTime.Exact(new DateTimeOffset(2026, 1, 15, 9, 0, 0, Est), winter);
        AssertTime.Exact(new DateTimeOffset(2026, 7, 15, 9, 0, 0, Edt), summer);
    }

    // ---------------------------------------------------------------------------------------
    // Agreement with the closed-form count. These two must never be able to disagree: the
    // sequence is the boundaries CountBoundaries counts, one per element.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheNumberOfBoundariesAlwaysEqualsCountBoundaries()
    {
        RecurrenceSchedule[] schedules =
        [
            RecurrenceSchedule.Daily(new TimeOnly(4, 30)),
            RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Daily(new TimeOnly(2, 15), TestTimeZones.LordHowe),
            RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30), TestTimeZones.NewYork),
            RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0), TestTimeZones.NewYork),
            RecurrenceSchedule.Monthly(1, new TimeOnly(1, 30), TestTimeZones.NewYork),
        ];

        // A year that contains both US transitions and both Lord Howe ones, sampled at an interval
        // that is coprime with a day so the endpoints land at every hour of the clock in turn.
        var origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        foreach (var schedule in schedules)
        {
            for (var minutes = 0; minutes <= 60 * 24 * 365; minutes += 60 * 24 * 11 / 2)
            {
                var from = origin.AddMinutes(minutes);
                var to = from.AddDays(37).AddMinutes(minutes % 1_440);

                Assert.Equal(
                    schedule.CountBoundaries(from, to),
                    schedule.EnumerateBoundaries(from, to).Count());
            }
        }
    }

    [Fact]
    public void EveryYieldedBoundaryLiesStrictlyAfterFromAndAtOrBeforeTo()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var from = new DateTimeOffset(2026, 3, 1, 5, 17, 0, Est);
        var to = new DateTimeOffset(2026, 11, 5, 5, 17, 0, Est);

        DateTimeOffset? previous = null;

        foreach (var boundary in schedule.EnumerateBoundaries(from, to))
        {
            Assert.True(boundary > from);
            Assert.True(boundary <= to);
            Assert.True(previous is null || boundary > previous);
            AssertTime.Exact(boundary, schedule.PreviousBoundary(boundary));
            previous = boundary;
        }

        Assert.NotNull(previous);
    }

    [Fact]
    public void TheSequenceAgreesWithWalkingNextBoundaryByHand()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 15), TestTimeZones.LordHowe);
        var from = new DateTimeOffset(2026, 3, 20, 12, 0, 0, LordHoweDaylight);
        var to = new DateTimeOffset(2026, 10, 20, 12, 0, 0, LordHoweDaylight);

        var walked = new List<DateTimeOffset>();
        for (var cursor = schedule.NextBoundary(from); cursor <= to; cursor = schedule.NextBoundary(cursor))
        {
            walked.Add(cursor);
        }

        var enumerated = schedule.EnumerateBoundaries(from, to).ToList();

        Assert.Equal(walked.Count, enumerated.Count);
        for (var i = 0; i < walked.Count; i++)
        {
            AssertTime.Exact(walked[i], enumerated[i]);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Daylight saving.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ASkippedWallClockTimeIsYieldedAtTheTransition_AndTheDayKeepsItsBoundary()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);

        var boundaries = schedule.EnumerateBoundaries(
            new DateTimeOffset(2026, 3, 6, 12, 0, 0, Est),
            new DateTimeOffset(2026, 3, 10, 12, 0, 0, Edt)).ToList();

        Assert.Equal(4, boundaries.Count);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 7, 2, 30, 0, Est), boundaries[0]);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 8, 3, 0, 0, Edt), boundaries[1]); // the transition
        AssertTime.Exact(new DateTimeOffset(2026, 3, 9, 2, 30, 0, Edt), boundaries[2]);
        AssertTime.Exact(new DateTimeOffset(2026, 3, 10, 2, 30, 0, Edt), boundaries[3]);
    }

    [Fact]
    public void ARepeatedWallClockTimeIsYieldedOnce_AtThePreTransitionOffset()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(1, 30), TestTimeZones.NewYork);

        var boundaries = schedule.EnumerateBoundaries(
            new DateTimeOffset(2026, 10, 30, 12, 0, 0, Edt),
            new DateTimeOffset(2026, 11, 2, 12, 0, 0, Est)).ToList();

        Assert.Equal(3, boundaries.Count);
        AssertTime.Exact(new DateTimeOffset(2026, 10, 31, 1, 30, 0, Edt), boundaries[0]);
        AssertTime.Exact(new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt), boundaries[1]); // the first 01:30
        AssertTime.Exact(new DateTimeOffset(2026, 11, 2, 1, 30, 0, Est), boundaries[2]);
    }

    [Fact]
    public void AThirtyMinuteTransitionIsHandledToo()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 15), TestTimeZones.LordHowe);

        var boundaries = schedule.EnumerateBoundaries(
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, LordHoweStandard),
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, LordHoweDaylight)).ToList();

        Assert.Equal(2, boundaries.Count);
        AssertTime.Exact(new DateTimeOffset(2026, 10, 4, 2, 30, 0, LordHoweDaylight), boundaries[0]);
        AssertTime.Exact(new DateTimeOffset(2026, 10, 5, 2, 15, 0, LordHoweDaylight), boundaries[1]);
    }

    // ---------------------------------------------------------------------------------------
    // Deferred execution.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void NothingIsComputedUntilTheSequenceIsEnumerated()
    {
        // An interval whose lower bound sits within a boundary of DateTimeOffset.MinValue asks for
        // an unrepresentable boundary. The call itself must still be silent — the throw belongs to
        // the first MoveNext.
        var sequence = Utc0430.EnumerateBoundaries(DateTimeOffset.MinValue, Utc(25, 12));

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.ToList());
    }

    [Fact]
    public void TheSequenceCanBeEnumeratedMoreThanOnce()
    {
        var sequence = Utc0430.EnumerateBoundaries(Utc(24, 12), Utc(27, 12));

        Assert.Equal(sequence.ToList(), sequence.ToList());
        Assert.Equal(3, sequence.Count());
    }

    [Fact]
    public void AnUnboundedUpperEndCanBeCutShortWithTake()
    {
        // The sequence is lazy, so a caller can ask for the next few boundaries without paying for
        // the whole interval.
        DateTimeOffset[] expected = [Utc(26, 4, 30), Utc(27, 4, 30), Utc(28, 4, 30)];

        Assert.Equal(
            expected,
            Utc0430.EnumerateBoundaries(Utc(25, 12), DateTimeOffset.MaxValue).Take(3).ToArray());
    }
}
