using SsalKit.Timekeeping;
using static SampleContext;

// [TimeWindow]
internal static class TimeWindowSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 5. TimeWindow arithmetic. Half-open [Start, End) is the only containment rule there is, which
        //    is what lets consecutive windows tile the timeline with neither overlap nor gap.
        // ---------------------------------------------------------------------------------------
        // WindowAt(now, -1) is the previous reset period -- the "compared to yesterday" window, reached by
        // occurrence arithmetic rather than by stepping, so WindowAt(now, -30) costs the same.
        var yesterday = DailyReset.WindowAt(Now, -1);
        var maintenance = new TimeWindow(
            new DateTimeOffset(2026, 7, 25, 3, 0, 0, Kst),
            new DateTimeOffset(2026, 7, 25, 6, 0, 0, Kst));
        var overrun = new DateTimeOffset(2026, 7, 27, 0, 0, 0, Kst);

        Console.WriteLine("[TimeWindow]     today's reset window and its neighbour");
        Console.WriteLine($"                 yesterday            [{Instant(yesterday.Start)}, {Instant(yesterday.End)})   <- WindowAt(now, -1)");
        Console.WriteLine($"                 today                [{Instant(Today.Start)}, {Instant(Today.End)})   <- WindowAt(now, 0) == CurrentWindow(now)");
        Console.WriteLine($"                 a week ago           [{Instant(DailyReset.WindowAt(Now, -7).Start)}, {Instant(DailyReset.WindowAt(Now, -7).End)})");
        Console.WriteLine($"                 they meet exactly    yesterday.End == today.Start: {yesterday.End == Today.Start}");
        Console.WriteLine($"                 and never overlap    Overlaps: {yesterday.Overlaps(Today)}");
        Console.WriteLine($"                 the shared boundary belongs to today: yesterday.Contains {yesterday.Contains(Today.Start)} / today.Contains {Today.Contains(Today.Start)}");
        Console.WriteLine($"                 Contains(now)        {Today.Contains(Now)}");
        Console.WriteLine($"                 maintenance window   [{Instant(maintenance.Start)}, {Instant(maintenance.End)})");

        var overlap = Today.Intersect(maintenance);
        Console.WriteLine($"                 Intersect            {(overlap is { } shared ? $"[{Instant(shared.Start)}, {Instant(shared.End)})  ({Elapsed(shared.Duration)} of downtime falls in today's window)" : "none")}");
        Console.WriteLine($"                 Clamp({Instant(overrun)})  -> {Instant(Today.Clamp(overrun))}  (an overrun clamps to the end)");

        // Offsets are display only: comparison and equality are by absolute instant, so the same window
        // written in UTC is the same window.
        var todayInUtc = new TimeWindow(Today.Start.ToUniversalTime(), Today.End.ToUniversalTime());
        Console.WriteLine($"                 same window in UTC   [{Instant(todayInUtc.Start)}, {Instant(todayInUtc.End)})  equal: {todayInUtc == Today}");
        Console.WriteLine();
    }
}
