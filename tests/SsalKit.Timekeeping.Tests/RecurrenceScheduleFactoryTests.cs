namespace SsalKit.Timekeeping.Tests;

public sealed class RecurrenceScheduleFactoryTests
{
    private static readonly TimeOnly Midnight = new(0, 0);

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Weekly_Rejects_UndefinedDayOfWeek(int dayOfWeek)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RecurrenceSchedule.Weekly((DayOfWeek)dayOfWeek, Midnight));

        Assert.Equal("dayOfWeek", exception.ParamName);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Saturday)]
    public void Weekly_Accepts_EveryDefinedDayOfWeek(DayOfWeek dayOfWeek)
    {
        var schedule = RecurrenceSchedule.Weekly(dayOfWeek, Midnight);

        Assert.Equal(dayOfWeek, schedule.NextBoundary(new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero)).DayOfWeek);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32)]
    [InlineData(int.MinValue)]
    public void Monthly_Rejects_DayOfMonthOutsideOneToThirtyOne(int dayOfMonth)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RecurrenceSchedule.Monthly(dayOfMonth, Midnight));

        Assert.Equal("dayOfMonth", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Monthly_Accepts_TheInclusiveBounds(int dayOfMonth)
    {
        var schedule = RecurrenceSchedule.Monthly(dayOfMonth, Midnight);

        Assert.Equal(dayOfMonth, schedule.NextBoundary(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)).Day);
    }

    [Fact]
    public void Factories_DefaultToUtc()
    {
        var asOf = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 26, 4, 30, 0, TimeSpan.Zero),
            RecurrenceSchedule.Daily(new TimeOnly(4, 30)).NextBoundary(asOf));
        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 27, 4, 30, 0, TimeSpan.Zero),
            RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(4, 30)).NextBoundary(asOf));
        AssertTime.Exact(
            new DateTimeOffset(2026, 8, 10, 4, 30, 0, TimeSpan.Zero),
            RecurrenceSchedule.Monthly(10, new TimeOnly(4, 30)).NextBoundary(asOf));
    }

    [Fact]
    public void Factories_HonourAnExplicitTimeZone()
    {
        var asOf = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.FromHours(9));

        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 26, 4, 30, 0, TimeSpan.FromHours(9)),
            RecurrenceSchedule.Daily(new TimeOnly(4, 30), TestTimeZones.Seoul).NextBoundary(asOf));
        AssertTime.Exact(
            new DateTimeOffset(2026, 7, 27, 4, 30, 0, TimeSpan.FromHours(9)),
            RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(4, 30), TestTimeZones.Seoul).NextBoundary(asOf));
        AssertTime.Exact(
            new DateTimeOffset(2026, 8, 10, 4, 30, 0, TimeSpan.FromHours(9)),
            RecurrenceSchedule.Monthly(10, new TimeOnly(4, 30), TestTimeZones.Seoul).NextBoundary(asOf));
    }
}
