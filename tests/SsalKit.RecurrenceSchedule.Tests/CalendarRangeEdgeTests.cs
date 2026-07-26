namespace SsalKit.RecurrenceSchedule.Tests;

/// <summary>
/// The top of the calendar, where the occurrence <i>after</i> the one being looked at does not
/// exist. Two things must hold there: the API behaves identically whether or not
/// <see cref="System.Diagnostics.Debug"/> assertions are compiled in, and
/// <see cref="RecurrenceSchedule.EnumerateBoundaries"/> keeps its promise of yielding exactly
/// <see cref="RecurrenceSchedule.CountBoundaries"/> elements rather than walking off the calendar.
/// </summary>
public sealed class CalendarRangeEdgeTests
{
    private static readonly RecurrenceSchedule[] Schedules =
    [
        RecurrenceSchedule.Daily(new TimeOnly(4, 30)),
        RecurrenceSchedule.Weekly(DayOfWeek.Friday, new TimeOnly(4, 30)),
        RecurrenceSchedule.Monthly(1, new TimeOnly(4, 30)),
        RecurrenceSchedule.Monthly(31, new TimeOnly(4, 30)),
    ];

    private static readonly DateTimeOffset NearTheEnd = new(9999, 6, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// <see cref="RecurrenceSchedule.CountBoundaries"/> up to <see cref="DateTimeOffset.MaxValue"/>
    /// locates the last representable occurrence, whose successor is off the calendar. Reaching for
    /// that successor — as an unguarded debug assertion once did — makes a Debug build throw where a
    /// Release build returns an answer, which is the one difference between the two that must never
    /// exist.
    /// </summary>
    [Fact]
    public void CountingUpToTheEndOfTheCalendarDoesNotThrow()
    {
        foreach (var schedule in Schedules)
        {
            Assert.True(schedule.CountBoundaries(NearTheEnd, DateTimeOffset.MaxValue) > 0);
            AssertTime.Exact(
                schedule.PreviousBoundary(DateTimeOffset.MaxValue),
                schedule.PreviousBoundary(schedule.PreviousBoundary(DateTimeOffset.MaxValue)));
            Assert.True(schedule.HasCrossed(NearTheEnd, DateTimeOffset.MaxValue));
            Assert.True(schedule.CurrentWindow(NearTheEnd).Contains(NearTheEnd));
        }
    }

    /// <summary>
    /// <c>EnumerateBoundaries(from, DateTimeOffset.MaxValue)</c> is the idiom the documentation and
    /// the sample both put forward, and it has to terminate: no boundary can ever exceed
    /// <see cref="DateTimeOffset.MaxValue"/>, so the sequence ends by running out of calendar rather
    /// than by overshooting its upper bound.
    /// </summary>
    [Fact]
    public void EnumeratingUpToTheEndOfTheCalendarTerminatesAndAgreesWithTheCount()
    {
        foreach (var schedule in Schedules)
        {
            var boundaries = schedule.EnumerateBoundaries(NearTheEnd, DateTimeOffset.MaxValue).ToList();

            Assert.Equal(schedule.CountBoundaries(NearTheEnd, DateTimeOffset.MaxValue), boundaries.Count);
            Assert.NotEmpty(boundaries);
            Assert.All(boundaries, boundary => Assert.True(boundary > NearTheEnd));
            AssertTime.Exact(schedule.PreviousBoundary(DateTimeOffset.MaxValue), boundaries[^1]);
        }
    }

    /// <summary>
    /// The last boundary of all is the one on the last representable date, and it is still a
    /// well-formed window opener.
    /// </summary>
    [Fact]
    public void TheFinalBoundaryIsTheOneOnTheLastRepresentableOccurrence()
    {
        var daily = RecurrenceSchedule.Daily(new TimeOnly(4, 30));
        var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(4, 30));

        AssertTime.Exact(
            new DateTimeOffset(9999, 12, 31, 4, 30, 0, TimeSpan.Zero),
            daily.EnumerateBoundaries(NearTheEnd, DateTimeOffset.MaxValue).Last());
        AssertTime.Exact(
            new DateTimeOffset(9999, 12, 31, 4, 30, 0, TimeSpan.Zero),
            monthly.EnumerateBoundaries(NearTheEnd, DateTimeOffset.MaxValue).Last());
    }
}
