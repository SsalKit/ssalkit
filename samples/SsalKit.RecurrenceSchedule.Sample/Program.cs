// SsalKit.RecurrenceSchedule sample
//
// Walks through the library in the order the questions usually come up: has the daily reset
// happened since we last looked, how many resets did a player miss while they were away, what do
// weekly and monthly cadences look like (including the month-end clamp), what happens on the two
// days a year a wall clock misbehaves, how the half-open TimeWindow arithmetic composes, and how
// the TimeProvider overloads read "now" for code that already holds a clock.
//
// Every instant below is a fixed literal rather than DateTimeOffset.UtcNow: the whole API is a
// pure function of (schedule, instant), so this output is byte-for-byte reproducible from run to
// run, and the daylight-saving section can sit on the exact 2026 transition dates.

using System.Globalization;
using SsalKit.RecurrenceSchedule;

// Formatting only: keeps the month names and separators below identical on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

var kst = TimeSpan.FromHours(9);
var est = TimeSpan.FromHours(-5); // New York, standard time
var edt = TimeSpan.FromHours(-4); // New York, daylight saving time

Console.WriteLine("== SsalKit.RecurrenceSchedule sample ==");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 1. Daily quota reset: "has the 04:30 Seoul reset happened since the last time we looked?"
//    HasCrossed answers exactly that -- it asks whether a boundary b satisfies
//    lastSeen < b <= now -- and NextBoundary says how long the current allowance has left.
// ---------------------------------------------------------------------------------------
var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), seoul);

var now = new DateTimeOffset(2026, 7, 25, 9, 15, 0, kst);
var lastReset = new DateTimeOffset(2026, 7, 24, 20, 0, 0, kst);

var today = dailyReset.CurrentWindow(now);
bool crossed = dailyReset.HasCrossed(lastReset, now);
var nextReset = dailyReset.NextBoundary(now);

Console.WriteLine("[Daily reset]    schedule: every day at 04:30 Asia/Seoul");
Console.WriteLine($"                 now                  {Instant(now)}");
Console.WriteLine($"                 last quota reset     {Instant(lastReset)}");
Console.WriteLine($"                 current window       [{Instant(today.Start)}, {Instant(today.End)})");
Console.WriteLine($"                 HasCrossed           {crossed}  -> {(crossed ? "refill the quota" : "leave the quota alone")}");
Console.WriteLine($"                 next reset           {Instant(nextReset)}  (in {Elapsed(nextReset - now)})");
Console.WriteLine();

// A last-seen instant that is already inside today's window has not crossed anything: a boundary
// belongs to the window it opens, so lastSeen == b means "that reset was already applied".
var alreadySeen = new DateTimeOffset(2026, 7, 25, 5, 0, 0, kst);
Console.WriteLine($"                 last seen {Instant(alreadySeen)} -> HasCrossed {dailyReset.HasCrossed(alreadySeen, now)} (same window, nothing to do)");

// And minutes are not negotiable. The prototype this library replaces compared hour fields
// (`from.Hour >= resetHour`), so 04:15 counted as past an 04:30 reset because 4 >= 4. Comparing
// instants makes that whole bug class unrepresentable.
var justBefore = new DateTimeOffset(2026, 7, 25, 4, 15, 0, kst);
Console.WriteLine($"                 04:00 -> 04:15       HasCrossed {dailyReset.HasCrossed(justBefore.AddMinutes(-15), justBefore)}  (an hour-field comparison would say True)");
Console.WriteLine($"                 04:00 -> 04:30       HasCrossed {dailyReset.HasCrossed(justBefore.AddMinutes(-15), justBefore.AddMinutes(15))}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 2. Missed daily rewards: CountBoundaries counts the boundaries in (lastSeen, now], which is
//    exactly how many daily grants a returning player is owed. It is closed-form calendar
//    arithmetic, not a loop, so a decade-wide gap costs the same as a one-day gap.
// ---------------------------------------------------------------------------------------
var lastLogin = new DateTimeOffset(2026, 7, 20, 22, 0, 0, kst);
int missedRewards = dailyReset.CountBoundaries(lastLogin, now);

Console.WriteLine("[Missed rewards] player returns after a few days away");
Console.WriteLine($"                 last login           {Instant(lastLogin)}");
Console.WriteLine($"                 now                  {Instant(now)}");
Console.WriteLine($"                 CountBoundaries      {missedRewards}  -> grant {missedRewards} days of rewards");

// Ten years, twenty daylight-saving transitions, one call: 365 * 10 + 3 leap days.
var decadeStart = new DateTimeOffset(2020, 1, 1, 0, 0, 0, est);
var decadeEnd = new DateTimeOffset(2030, 1, 1, 0, 0, 0, est);
var midnightNewYork = RecurrenceSchedule.Daily(new TimeOnly(0, 0), newYork);
Console.WriteLine($"                 2020-01-01 -> 2030-01-01 (New York midnight): {midnightNewYork.CountBoundaries(decadeStart, decadeEnd)} boundaries, computed in O(1)");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 3. Weekly and monthly cadences. A monthly schedule anchored to day 31 clamps to the last day
//    of shorter months, which is what keeps "exactly one boundary per month" true -- a schedule
//    that simply skipped February would make CountBoundaries lie.
// ---------------------------------------------------------------------------------------
var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));
var weeklyAsOf = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero); // a Saturday
var weeklyWindow = weekly.CurrentWindow(weeklyAsOf);

Console.WriteLine("[Weekly]         schedule: every Monday at 09:00 UTC");
Console.WriteLine($"                 as of                {Instant(weeklyAsOf)}");
Console.WriteLine($"                 current window       [{Instant(weeklyWindow.Start)}, {Instant(weeklyWindow.End)})  ({Elapsed(weeklyWindow.Duration)})");
Console.WriteLine();

var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));
var cursor = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

Console.WriteLine("[Monthly]        schedule: the 31st of every month at 00:00 UTC");
Console.WriteLine($"                 the next four boundaries after {Instant(cursor)}:");
for (int i = 0; i < 4; i++)
{
    cursor = monthly.NextBoundary(cursor);
    Console.WriteLine($"                   {Instant(cursor)}  ({cursor:MMMM} has {DateTime.DaysInMonth(cursor.Year, cursor.Month)} days)");
}

Console.WriteLine($"                 boundaries in 2026   {monthly.CountBoundaries(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero))}  (never skips a short month)");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 4. Daylight saving. The scheduled time is a wall-clock time, and a wall clock can misbehave in
//    exactly two ways. Both resolutions are a fixed versioned contract -- boundaries get
//    persisted, so they can never be changed in a patch or minor release.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[DST rule 1]     a scheduled time that never happens -> the first instant after the gap");

// 2026-03-08 in New York: 02:00 EST becomes 03:00 EDT, so 02:30 does not exist that day.
var springSchedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), newYork);
var springDay = new DateTimeOffset(2026, 3, 8, 12, 0, 0, edt);
var springBoundary = springSchedule.PreviousBoundary(springDay);

Console.WriteLine("                 schedule: every day at 02:30 America/New_York");
Console.WriteLine("                 2026-03-08: 02:00 EST jumps to 03:00 EDT, so 02:30 is skipped");
Console.WriteLine($"                 boundary on 03-07     {Instant(springSchedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, est)))}");
Console.WriteLine($"                 boundary on 03-08     {Instant(springBoundary)}  <- the transition itself, not 03:30");
Console.WriteLine($"                 boundary on 03-09     {Instant(springSchedule.NextBoundary(springDay))}");
Console.WriteLine($"                 the day keeps its boundary: 03-07 12:00 -> 03-09 12:00 crosses {springSchedule.CountBoundaries(new DateTimeOffset(2026, 3, 7, 12, 0, 0, est), new DateTimeOffset(2026, 3, 9, 12, 0, 0, edt))} boundaries");
Console.WriteLine($"                 the window shortens instead: {Elapsed(springSchedule.CurrentWindow(springDay).Duration)} rather than 24h");
Console.WriteLine();

Console.WriteLine("[DST rule 2]     a scheduled time that happens twice -> the first occurrence only");

// 2026-11-01 in New York: 02:00 EDT becomes 01:00 EST, so 01:30 happens twice.
var autumnSchedule = RecurrenceSchedule.Daily(new TimeOnly(1, 30), newYork);
var firstOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, edt);
var secondOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, est);

Console.WriteLine("                 schedule: every day at 01:30 America/New_York");
Console.WriteLine("                 2026-11-01: 02:00 EDT falls back to 01:00 EST, so 01:30 happens twice");
Console.WriteLine($"                 first 01:30           {Instant(firstOccurrence)}  = {firstOccurrence.UtcDateTime:HH:mm}Z");
Console.WriteLine($"                 second 01:30          {Instant(secondOccurrence)}  = {secondOccurrence.UtcDateTime:HH:mm}Z");
Console.WriteLine($"                 boundary on 11-01     {Instant(autumnSchedule.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, est)))}  <- the first one");
Console.WriteLine($"                 first -> second 01:30 crosses {autumnSchedule.CountBoundaries(firstOccurrence, secondOccurrence)} boundaries: the schedule does not fire twice");
Console.WriteLine($"                 the window lengthens instead: {Elapsed(autumnSchedule.CurrentWindow(secondOccurrence).Duration)} rather than 24h");
Console.WriteLine();

Console.WriteLine("[DST rule 3]     every other wall-clock time keeps its local time year-round");
var nineAm = RecurrenceSchedule.Daily(new TimeOnly(9, 0), newYork);
Console.WriteLine($"                 09:00 in January      {Instant(nineAm.PreviousBoundary(new DateTimeOffset(2026, 1, 15, 12, 0, 0, est)))}");
Console.WriteLine($"                 09:00 in July         {Instant(nineAm.PreviousBoundary(new DateTimeOffset(2026, 7, 15, 12, 0, 0, edt)))}");
Console.WriteLine($"                 boundaries in 2026    {nineAm.CountBoundaries(new DateTimeOffset(2026, 1, 1, 9, 0, 0, est), new DateTimeOffset(2027, 1, 1, 9, 0, 0, est))}  (one per calendar day, transitions included)");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 5. TimeWindow arithmetic. Half-open [Start, End) is the only containment rule there is, which
//    is what lets consecutive windows tile the timeline with neither overlap nor gap.
// ---------------------------------------------------------------------------------------
var yesterday = dailyReset.CurrentWindow(today.Start.AddTicks(-1));
var maintenance = new TimeWindow(
    new DateTimeOffset(2026, 7, 25, 3, 0, 0, kst),
    new DateTimeOffset(2026, 7, 25, 6, 0, 0, kst));
var overrun = new DateTimeOffset(2026, 7, 27, 0, 0, 0, kst);

Console.WriteLine("[TimeWindow]     today's reset window and its neighbour");
Console.WriteLine($"                 yesterday            [{Instant(yesterday.Start)}, {Instant(yesterday.End)})");
Console.WriteLine($"                 today                [{Instant(today.Start)}, {Instant(today.End)})");
Console.WriteLine($"                 they meet exactly    yesterday.End == today.Start: {yesterday.End == today.Start}");
Console.WriteLine($"                 and never overlap    Overlaps: {yesterday.Overlaps(today)}");
Console.WriteLine($"                 the shared boundary belongs to today: yesterday.Contains {yesterday.Contains(today.Start)} / today.Contains {today.Contains(today.Start)}");
Console.WriteLine($"                 Contains(now)        {today.Contains(now)}");
Console.WriteLine($"                 maintenance window   [{Instant(maintenance.Start)}, {Instant(maintenance.End)})");

var overlap = today.Intersect(maintenance);
Console.WriteLine($"                 Intersect            {(overlap is { } shared ? $"[{Instant(shared.Start)}, {Instant(shared.End)})  ({Elapsed(shared.Duration)} of downtime falls in today's window)" : "none")}");
Console.WriteLine($"                 Clamp({Instant(overrun)})  -> {Instant(today.Clamp(overrun))}  (an overrun clamps to the end)");

// Offsets are display only: comparison and equality are by absolute instant, so the same window
// written in UTC is the same window.
var todayInUtc = new TimeWindow(today.Start.ToUniversalTime(), today.End.ToUniversalTime());
Console.WriteLine($"                 same window in UTC   [{Instant(todayInUtc.Start)}, {Instant(todayInUtc.End)})  equal: {todayInUtc == today}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 6. TimeProvider overloads. The core API always takes the instant as an argument -- that is
//    what makes it testable without freezing a global clock -- and these extensions are sugar
//    over it for callers that already have an injected clock. TimeProvider is BCL from .NET 8,
//    so using them adds no package dependency.
// ---------------------------------------------------------------------------------------
TimeProvider clock = new FixedTimeProvider(now);

Console.WriteLine("[TimeProvider]   the same four questions, with 'now' read from an injected clock");
Console.WriteLine($"                 clock.GetUtcNow()    {Instant(clock.GetUtcNow())}");
Console.WriteLine($"                 NextBoundary(clock)  {Instant(dailyReset.NextBoundary(clock))}");
Console.WriteLine($"                 CurrentWindow(clock) [{Instant(dailyReset.CurrentWindow(clock).Start)}, {Instant(dailyReset.CurrentWindow(clock).End)})");
Console.WriteLine($"                 HasCrossed(clock)    {dailyReset.HasCrossed(lastReset, clock)}");
Console.WriteLine($"                 CountBoundaries      {dailyReset.CountBoundaries(lastLogin, clock)}");
Console.WriteLine();
Console.WriteLine("                 In tests, hand in a fake provider (FakeTimeProvider, or the handful of");
Console.WriteLine("                 lines FixedTimeProvider takes at the bottom of this file) to drive a");
Console.WriteLine("                 schedule across a boundary deterministically.");

// Renders an instant with the UTC offset it carries. Boundaries come back at the schedule time
// zone's offset for that date, which is exactly what makes the DST section above readable: the
// same wall-clock schedule shows as -05:00 in winter and -04:00 in summer.
static string Instant(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss zzz");

// Renders a duration in whole days once it spans more than a couple of them, and in hours and
// minutes below that -- which is the resolution the daylight-saving windows need.
static string Elapsed(TimeSpan value) => (value.TotalHours, value.Hours, value.Minutes) switch
{
    ( >= 48, 0, 0) => $"{(int)value.TotalDays} days",
    (_, _, 0) => $"{(int)value.TotalHours}h",
    _ => $"{(int)value.TotalHours}h {value.Minutes:D2}m",
};

// The whole of a test clock: the extension methods only ever call GetUtcNow(), so there is
// nothing else to fake. Microsoft.Extensions.TimeProvider.Testing's FakeTimeProvider works just
// as well and can also advance time.
sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
{
    private readonly DateTimeOffset _utcNow = instant.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
