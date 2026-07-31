using SsalKit.Timekeeping;

// Fixed instants, schedules, and rendering helpers shared across the sample groups. Every instant
// here is a literal rather than DateTimeOffset.UtcNow: the whole API is a pure function of
// (schedule, instant), so the sample's output is byte-for-byte reproducible from run to run --
// regardless of which groups are selected to run.
internal static class SampleContext
{
    public static readonly TimeZoneInfo Seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
    public static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static readonly TimeSpan Kst = TimeSpan.FromHours(9);
    public static readonly TimeSpan Est = TimeSpan.FromHours(-5); // New York, standard time
    public static readonly TimeSpan Edt = TimeSpan.FromHours(-4); // New York, daylight saving time

    // The daily quota reset schedule, and the instants around it: shared by the recurrence,
    // TimeWindow, TimeProvider, and combined groups.
    public static readonly RecurrenceSchedule DailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), Seoul);

    public static readonly DateTimeOffset Now = new(2026, 7, 25, 9, 15, 0, Kst);
    public static readonly DateTimeOffset LastReset = new(2026, 7, 24, 20, 0, 0, Kst);
    public static readonly DateTimeOffset LastLogin = new(2026, 7, 20, 22, 0, 0, Kst);

    public static readonly TimeWindow Today = DailyReset.CurrentWindow(Now);

    // Renders an instant with the UTC offset it carries. Boundaries come back at the schedule time
    // zone's offset for that date, which is exactly what makes the daylight-saving group readable:
    // the same wall-clock schedule shows as -05:00 in winter and -04:00 in summer.
    public static string Instant(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss zzz");

    // Renders a duration in whole days once it spans more than a couple of them, and in hours and
    // minutes below that -- which is the resolution the daylight-saving windows need.
    public static string Elapsed(TimeSpan value) => (value.TotalHours, value.Hours, value.Minutes) switch
    {
        ( >= 48, 0, 0) => $"{(int)value.TotalDays} days",
        (_, _, 0) => $"{(int)value.TotalHours}h",
        _ => $"{(int)value.TotalHours}h {value.Minutes:D2}m",
    };
}
