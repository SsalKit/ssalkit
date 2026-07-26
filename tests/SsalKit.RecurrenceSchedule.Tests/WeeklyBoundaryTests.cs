namespace SsalKit.RecurrenceSchedule.Tests;

public sealed class WeeklyBoundaryTests
{
    // 2026-07-25 is a Saturday, so 2026-07-20 and 2026-07-27 are Mondays and 2026-07-19 and
    // 2026-07-26 are Sundays.
    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(2026, 7, day, hour, minute, 0, TimeSpan.Zero);

    private static readonly RecurrenceSchedule Mondays = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));

    [Fact]
    public void Boundaries_FallOnTheScheduledDayOfWeek()
    {
        AssertTime.Exact(Utc(20, 9), Mondays.PreviousBoundary(Utc(25, 12)));
        AssertTime.Exact(Utc(27, 9), Mondays.NextBoundary(Utc(25, 12)));
        Assert.Equal(DayOfWeek.Monday, Mondays.PreviousBoundary(Utc(25, 12)).DayOfWeek);
    }

    [Fact]
    public void OnTheScheduledDay_TheTimeOfDayStillDecides()
    {
        // Before the scheduled time on a Monday the current window is still the previous week's.
        AssertTime.Exact(Utc(13, 9), Mondays.PreviousBoundary(Utc(20, 8, 59)));
        AssertTime.Exact(Utc(20, 9), Mondays.NextBoundary(Utc(20, 8, 59)));

        AssertTime.Exact(Utc(20, 9), Mondays.PreviousBoundary(Utc(20, 9)));
        AssertTime.Exact(Utc(27, 9), Mondays.NextBoundary(Utc(20, 9)));
    }

    [Fact]
    public void EveryDayOfWeek_WrapsCorrectly()
    {
        var sundays = RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(9, 0));
        var saturdays = RecurrenceSchedule.Weekly(DayOfWeek.Saturday, new TimeOnly(9, 0));

        // Sunday is day 0 of the week, so from a Saturday it looks "backwards" — this is where an
        // off-by-one in the day-of-week arithmetic would show up.
        AssertTime.Exact(Utc(19, 9), sundays.PreviousBoundary(Utc(25, 12)));
        AssertTime.Exact(Utc(26, 9), sundays.NextBoundary(Utc(25, 12)));

        AssertTime.Exact(Utc(25, 9), saturdays.PreviousBoundary(Utc(25, 12)));
        AssertTime.Exact(Utc(18, 9), saturdays.PreviousBoundary(Utc(25, 8)));
    }

    [Fact]
    public void CurrentWindow_IsSevenDaysLong()
    {
        var window = Mondays.CurrentWindow(Utc(25, 12));

        AssertTime.Exact(Utc(20, 9), window.Start);
        AssertTime.Exact(Utc(27, 9), window.End);
        Assert.Equal(TimeSpan.FromDays(7), window.Duration);
    }

    [Fact]
    public void CountBoundaries_CountsWeeksNotDays()
    {
        Assert.Equal(1, Mondays.CountBoundaries(Utc(20, 9), Utc(27, 9)));
        Assert.Equal(0, Mondays.CountBoundaries(Utc(20, 9), Utc(27, 9).AddTicks(-1)));
        Assert.Equal(
            10,
            Mondays.CountBoundaries(Utc(20, 9), new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void ADaylightSavingWeek_IsShorterOrLongerButStillOneBoundary()
    {
        var schedule = RecurrenceSchedule.Weekly(DayOfWeek.Wednesday, new TimeOnly(9, 0), TestTimeZones.NewYork);

        // The week containing the spring-forward Sunday loses an hour.
        var springWeek = schedule.CurrentWindow(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.FromHours(-5)));
        Assert.Equal(TimeSpan.FromDays(7) - TimeSpan.FromHours(1), springWeek.Duration);

        // The week containing the fall-back Sunday gains one.
        var autumnWeek = schedule.CurrentWindow(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(-4)));
        Assert.Equal(TimeSpan.FromDays(7) + TimeSpan.FromHours(1), autumnWeek.Duration);

        Assert.Equal(1, schedule.CountBoundaries(springWeek.Start, springWeek.End));
        Assert.Equal(1, schedule.CountBoundaries(autumnWeek.Start, autumnWeek.End));
    }
}
