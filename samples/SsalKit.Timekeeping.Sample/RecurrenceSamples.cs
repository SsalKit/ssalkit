using SsalKit.Timekeeping;
using static SampleContext;

// [Daily reset] [Missed rewards] [Weekly] [Monthly]
internal static class RecurrenceSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 1. Daily quota reset: "has the 04:30 Seoul reset happened since the last time we looked?"
        //    HasCrossed answers exactly that -- it asks whether a boundary b satisfies
        //    lastSeen < b <= now -- and UntilNext says how long the current allowance has left.
        // ---------------------------------------------------------------------------------------
        bool crossed = DailyReset.HasCrossed(LastReset, Now);
        var nextReset = DailyReset.NextBoundary(Now);

        // ToString() renders the schedule itself, for logs and debugger windows. It is a diagnostic
        // rendering, not a parsing contract -- unlike the daylight-saving rules, the format may improve.
        Console.WriteLine($"[Daily reset]    schedule: {DailyReset}  (ToString() is for logs, not for parsing)");
        Console.WriteLine($"                 now                  {Instant(Now)}");
        Console.WriteLine($"                 last quota reset     {Instant(LastReset)}");
        Console.WriteLine($"                 current window       [{Instant(Today.Start)}, {Instant(Today.End)})");
        Console.WriteLine($"                 HasCrossed           {crossed}  -> {(crossed ? "refill the quota" : "leave the quota alone")}");
        Console.WriteLine($"                 next reset           {Instant(nextReset)}  (in {Elapsed(DailyReset.UntilNext(Now))})");
        Console.WriteLine();

        // A last-seen instant that is already inside today's window has not crossed anything: a boundary
        // belongs to the window it opens, so lastSeen == b means "that reset was already applied".
        var alreadySeen = new DateTimeOffset(2026, 7, 25, 5, 0, 0, Kst);
        Console.WriteLine($"                 last seen {Instant(alreadySeen)} -> HasCrossed {DailyReset.HasCrossed(alreadySeen, Now)} (same window, nothing to do)");

        // And minutes are not negotiable. The prototype this library replaces compared hour fields
        // (`from.Hour >= resetHour`), so 04:15 counted as past an 04:30 reset because 4 >= 4. Comparing
        // instants makes that whole bug class unrepresentable.
        var justBefore = new DateTimeOffset(2026, 7, 25, 4, 15, 0, Kst);
        Console.WriteLine($"                 04:00 -> 04:15       HasCrossed {DailyReset.HasCrossed(justBefore.AddMinutes(-15), justBefore)}  (an hour-field comparison would say True)");
        Console.WriteLine($"                 04:00 -> 04:30       HasCrossed {DailyReset.HasCrossed(justBefore.AddMinutes(-15), justBefore.AddMinutes(15))}");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 2. Missed daily rewards: CountBoundaries counts the boundaries in (lastSeen, now], which is
        //    exactly how many daily grants a returning player is owed. It is closed-form calendar
        //    arithmetic, not a loop, so a decade-wide gap costs the same as a one-day gap.
        // ---------------------------------------------------------------------------------------
        int missedRewards = DailyReset.CountBoundaries(LastLogin, Now);

        Console.WriteLine("[Missed rewards] player returns after a few days away");
        Console.WriteLine($"                 last login           {Instant(LastLogin)}");
        Console.WriteLine($"                 now                  {Instant(Now)}");
        Console.WriteLine($"                 CountBoundaries      {missedRewards}  -> grant {missedRewards} days of rewards");

        // Ten years, twenty daylight-saving transitions, one call: 365 * 10 + 3 leap days.
        var decadeStart = new DateTimeOffset(2020, 1, 1, 0, 0, 0, Est);
        var decadeEnd = new DateTimeOffset(2030, 1, 1, 0, 0, 0, Est);
        var midnightNewYork = RecurrenceSchedule.Daily(new TimeOnly(0, 0), NewYork);
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

        Console.WriteLine($"[Weekly]         schedule: {weekly}");
        Console.WriteLine($"                 as of                {Instant(weeklyAsOf)}");
        Console.WriteLine($"                 current window       [{Instant(weeklyWindow.Start)}, {Instant(weeklyWindow.End)})  ({Elapsed(weeklyWindow.Duration)})");
        Console.WriteLine();

        var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));
        var cursor = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        // EnumerateBoundaries walks (from, to] lazily and in ascending order, so Take cuts it short
        // without paying for the rest of the interval.
        Console.WriteLine($"[Monthly]        schedule: {monthly}");
        Console.WriteLine($"                 the next four boundaries after {Instant(cursor)}:");
        foreach (var boundary in monthly.EnumerateBoundaries(cursor, DateTimeOffset.MaxValue).Take(4))
        {
            Console.WriteLine($"                   {Instant(boundary)}  ({boundary:MMMM} has {DateTime.DaysInMonth(boundary.Year, boundary.Month)} days)");
        }

        Console.WriteLine($"                 boundaries in 2026   {monthly.CountBoundaries(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero))}  (never skips a short month)");
        Console.WriteLine();
    }
}
