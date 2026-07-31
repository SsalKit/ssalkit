using System.Globalization;

namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// <see cref="RecurrenceSchedule.ToString"/> is documented as a diagnostic rendering rather than a
/// parsing contract, so these tests pin it only so far as the library's own promises go: every
/// component of the schedule shows up, the format does not move with the ambient culture, and the
/// three cadences are told apart.
/// </summary>
public sealed class ScheduleToStringTests
{
    [Fact]
    public void ADailySchedule_NamesItsCadenceTimeAndZone()
    {
        Assert.Equal("Daily 04:30 @ UTC", RecurrenceSchedule.Daily(new TimeOnly(4, 30)).ToString());
        Assert.Equal(
            "Daily 00:00 @ Asia/Seoul",
            RecurrenceSchedule.Daily(new TimeOnly(0, 0), TestTimeZones.Seoul).ToString());
    }

    [Fact]
    public void AWeeklySchedule_NamesItsDayOfWeek()
    {
        Assert.Equal(
            "Weekly Monday 09:00 @ UTC",
            RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0)).ToString());
        Assert.Equal(
            "Weekly Sunday 02:30 @ America/New_York",
            RecurrenceSchedule.Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30), TestTimeZones.NewYork).ToString());
    }

    [Fact]
    public void AMonthlySchedule_NamesItsDayOfMonth()
    {
        Assert.Equal(
            "Monthly day 31 00:00 @ America/New_York",
            RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0), TestTimeZones.NewYork).ToString());
        Assert.Equal(
            "Monthly day 1 23:59 @ UTC",
            RecurrenceSchedule.Monthly(1, new TimeOnly(23, 59)).ToString());
    }

    [Fact]
    public void TheTimeOfDayGrowsOnlyAsPreciseAsTheScheduleIs()
    {
        Assert.Equal("Daily 04:30 @ UTC", RecurrenceSchedule.Daily(new TimeOnly(4, 30)).ToString());
        Assert.Equal("Daily 23:59:59 @ UTC", RecurrenceSchedule.Daily(new TimeOnly(23, 59, 59)).ToString());
        Assert.Equal(
            "Daily 04:30:15.2500000 @ UTC",
            RecurrenceSchedule.Daily(new TimeOnly(4, 30, 15, 250)).ToString());
        Assert.Equal(
            "Daily 04:30:00.0000001 @ UTC",
            RecurrenceSchedule.Daily(new TimeOnly(4, 30).Add(TimeSpan.FromTicks(1))).ToString());
    }

    [Fact]
    public void TheRenderingDoesNotMoveWithTheAmbientCulture()
    {
        var schedules = new[]
        {
            RecurrenceSchedule.Daily(new TimeOnly(4, 30), TestTimeZones.Seoul),
            RecurrenceSchedule.Weekly(DayOfWeek.Wednesday, new TimeOnly(13, 5, 30)),
            RecurrenceSchedule.Monthly(15, new TimeOnly(0, 0), TestTimeZones.NewYork),
        };

        var invariant = schedules.Select(schedule => schedule.ToString()).ToArray();
        var original = CultureInfo.CurrentCulture;

        try
        {
            foreach (var name in new[] { "ko-KR", "ar-SA", "th-TH", "fa-IR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                Assert.Equal(invariant, schedules.Select(schedule => schedule.ToString()).ToArray());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void TheRenderingIsStableAndIndependentOfAnyInstant()
    {
        var schedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), TestTimeZones.NewYork);
        var first = schedule.ToString();

        // Nothing about a schedule changes as it is used, so neither does its description — winter
        // and summer offsets included.
        schedule.PreviousBoundary(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.FromHours(-5)));
        schedule.NextBoundary(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(-4)));

        Assert.Equal(first, schedule.ToString());
        Assert.Equal("Daily 02:30 @ America/New_York", first);
    }
}
