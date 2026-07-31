using SsalKit.Timekeeping;
using static SampleContext;

// [DST rule 1] [DST rule 2] [DST rule 4]
internal static class DstSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 4. Daylight saving. The scheduled time is a wall-clock time, and a wall clock can misbehave in
        //    exactly three ways. Every resolution is a fixed versioned contract -- boundaries get
        //    persisted, so they can never be changed in a patch or minor release.
        // ---------------------------------------------------------------------------------------
        Console.WriteLine("[DST rule 1]     a scheduled time that never happens -> the first instant after the gap");

        // 2026-03-08 in New York: 02:00 EST becomes 03:00 EDT, so 02:30 does not exist that day.
        var springSchedule = RecurrenceSchedule.Daily(new TimeOnly(2, 30), NewYork);
        var springDay = new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt);
        var springBoundary = springSchedule.PreviousBoundary(springDay);

        Console.WriteLine("                 schedule: every day at 02:30 America/New_York");
        Console.WriteLine("                 2026-03-08: 02:00 EST jumps to 03:00 EDT, so 02:30 is skipped");
        Console.WriteLine($"                 boundary on 03-07     {Instant(springSchedule.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, Est)))}");
        Console.WriteLine($"                 boundary on 03-08     {Instant(springBoundary)}  <- the transition itself, not 03:30");
        Console.WriteLine($"                 boundary on 03-09     {Instant(springSchedule.NextBoundary(springDay))}");
        Console.WriteLine($"                 the day keeps its boundary: 03-07 12:00 -> 03-09 12:00 crosses {springSchedule.CountBoundaries(new DateTimeOffset(2026, 3, 7, 12, 0, 0, Est), new DateTimeOffset(2026, 3, 9, 12, 0, 0, Edt))} boundaries");
        Console.WriteLine($"                 the window shortens instead: {Elapsed(springSchedule.CurrentWindow(springDay).Duration)} rather than 24h");
        Console.WriteLine();

        Console.WriteLine("[DST rule 2]     a scheduled time that happens twice -> the first occurrence only");

        // 2026-11-01 in New York: 02:00 EDT becomes 01:00 EST, so 01:30 happens twice.
        var autumnSchedule = RecurrenceSchedule.Daily(new TimeOnly(1, 30), NewYork);
        var firstOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt);
        var secondOccurrence = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Est);

        Console.WriteLine("                 schedule: every day at 01:30 America/New_York");
        Console.WriteLine("                 2026-11-01: 02:00 EDT falls back to 01:00 EST, so 01:30 happens twice");
        Console.WriteLine($"                 first 01:30           {Instant(firstOccurrence)}  = {firstOccurrence.UtcDateTime:HH:mm}Z");
        Console.WriteLine($"                 second 01:30          {Instant(secondOccurrence)}  = {secondOccurrence.UtcDateTime:HH:mm}Z");
        Console.WriteLine($"                 boundary on 11-01     {Instant(autumnSchedule.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est)))}  <- the first one");
        Console.WriteLine($"                 first -> second 01:30 crosses {autumnSchedule.CountBoundaries(firstOccurrence, secondOccurrence)} boundaries: the schedule does not fire twice");
        Console.WriteLine($"                 the window lengthens instead: {Elapsed(autumnSchedule.CurrentWindow(secondOccurrence).Duration)} rather than 24h");
        Console.WriteLine();

        // Rule 3 -- a wall-clock time swallowed by a permanent change of the zone's *base* offset, as
        // Libya's turn of 2012 or Samoa's skipped 30 December 2011 -- resolves by rule 1's principle: the
        // first instant at which the zone's wall clock reaches the scheduled time. It is not demonstrated
        // here because whether a given zone carries such a seam depends on the platform's time-zone data.
        Console.WriteLine("[DST rule 4]     every other wall-clock time keeps its local time year-round");
        var nineAm = RecurrenceSchedule.Daily(new TimeOnly(9, 0), NewYork);
        Console.WriteLine($"                 09:00 in January      {Instant(nineAm.PreviousBoundary(new DateTimeOffset(2026, 1, 15, 12, 0, 0, Est)))}");
        Console.WriteLine($"                 09:00 in July         {Instant(nineAm.PreviousBoundary(new DateTimeOffset(2026, 7, 15, 12, 0, 0, Edt)))}");
        Console.WriteLine($"                 boundaries in 2026    {nineAm.CountBoundaries(new DateTimeOffset(2026, 1, 1, 9, 0, 0, Est), new DateTimeOffset(2027, 1, 1, 9, 0, 0, Est))}  (one per calendar day, transitions included)");
        Console.WriteLine();
    }
}
