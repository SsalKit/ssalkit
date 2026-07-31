namespace SsalKit.Timekeeping.Tests;

public sealed class MonthlyBoundaryTests
{
    private static readonly RecurrenceSchedule LastDayish = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Boundaries_FallOnTheScheduledDayOfMonth()
    {
        var schedule = RecurrenceSchedule.Monthly(15, new TimeOnly(4, 30));

        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 15, 4, 30, 0, TimeSpan.Zero),
            schedule.PreviousBoundary(Utc(2026, 7, 25)));
        AssertTime.Exact(
            new DateTimeOffset(2026, 8, 15, 4, 30, 0, TimeSpan.Zero),
            schedule.NextBoundary(Utc(2026, 7, 25)));

        // Earlier in the month the current window is still the previous month's.
        AssertTime.Exact(
            new DateTimeOffset(2026, 6, 15, 4, 30, 0, TimeSpan.Zero),
            schedule.PreviousBoundary(Utc(2026, 7, 10)));

        // ...and so is the same day before the scheduled time.
        AssertTime.Exact(
            new DateTimeOffset(2026, 6, 15, 4, 30, 0, TimeSpan.Zero),
            schedule.PreviousBoundary(new DateTimeOffset(2026, 7, 15, 4, 29, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(2026, 1, 31)]   // 31-day month: no clamping
    [InlineData(2026, 2, 28)]   // common year February
    [InlineData(2028, 2, 29)]   // leap year February
    [InlineData(2026, 4, 30)]   // 30-day month
    [InlineData(2100, 2, 28)]   // a century year that is not a leap year
    public void ShortMonths_ClampToTheirLastDay(int year, int month, int expectedDay)
    {
        var boundary = LastDayish.NextBoundary(Utc(year, month, 1));

        AssertTime.Exact(new DateTimeOffset(year, month, expectedDay, 0, 0, 0, TimeSpan.Zero), boundary);
    }

    [Fact]
    public void ClampedMonths_StillGetExactlyOneBoundary()
    {
        Assert.Equal(12, LastDayish.CountBoundaries(Utc(2026, 1, 1), Utc(2027, 1, 1)));

        // February's single boundary is the 28th, and the March one is still the 31st.
        AssertTime.Exact(Utc(2026, 2, 28), LastDayish.PreviousBoundary(Utc(2026, 3, 15)));
        AssertTime.Exact(Utc(2026, 3, 31), LastDayish.NextBoundary(Utc(2026, 3, 15)));
    }

    [Fact]
    public void TheYearRollsOver()
    {
        AssertTime.Exact(Utc(2027, 1, 31), LastDayish.NextBoundary(Utc(2026, 12, 31)));
        AssertTime.Exact(Utc(2026, 12, 31), LastDayish.PreviousBoundary(Utc(2027, 1, 1)));

        var december = RecurrenceSchedule.Monthly(1, new TimeOnly(0, 0));
        AssertTime.Exact(Utc(2027, 1, 1), december.NextBoundary(Utc(2026, 12, 1)));
        AssertTime.Exact(Utc(2026, 12, 1), december.PreviousBoundary(Utc(2026, 12, 31)));
    }

    [Fact]
    public void CurrentWindow_SpansOneCalendarMonth()
    {
        var window = LastDayish.CurrentWindow(Utc(2026, 2, 10));

        AssertTime.Exact(Utc(2026, 1, 31), window.Start);
        AssertTime.Exact(Utc(2026, 2, 28), window.End);
        Assert.Equal(TimeSpan.FromDays(28), window.Duration);
    }

    [Fact]
    public void CountBoundaries_CountsMonthsNotDays()
    {
        Assert.Equal(1, LastDayish.CountBoundaries(Utc(2026, 1, 31), Utc(2026, 2, 28)));
        Assert.Equal(0, LastDayish.CountBoundaries(Utc(2026, 1, 31), Utc(2026, 2, 28).AddTicks(-1)));
        Assert.Equal(24, LastDayish.CountBoundaries(Utc(2026, 1, 31), Utc(2028, 1, 31)));
    }
}
