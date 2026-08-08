using SsalKit.Timekeeping;

// [TickCooldown] [Default] [Overflow] [TickLoop]
internal static class TickCooldownSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 16. TickCooldown: Cooldown moved onto the tick axis. Same immutable record struct, same
        //     (state, tick) pure functions, same inclusive boundary -- measured in the long the
        //     simulation already counts instead of in elapsed wall-clock time. Create is immediately
        //     usable, a successful TryUse starts a fresh DurationTicks-long wait, and the wait is over
        //     *at* the tick it completes, not only after it.
        // ---------------------------------------------------------------------------------------
        var dash = TickCooldown.Create(durationTicks: 5, asOfTick: 100);

        Console.WriteLine("[TickCooldown]   a 5-tick dash cooldown, created at tick 100");
        Console.WriteLine($"                 Create(5, 100)       ReadyAtTick {dash.ReadyAtTick}  (immediately usable)");

        bool firstDash = dash.TryUse(100, out var onCooldown);
        Console.WriteLine($"                 TryUse(100)          {firstDash}   -> ReadyAtTick {onCooldown.ReadyAtTick}");

        bool secondDash = onCooldown.TryUse(101, out _);
        Console.WriteLine($"                 TryUse(101)          {secondDash}  (still on cooldown)  Remaining {onCooldown.Remaining(101)} ticks");

        var countdown = string.Join(" ", Enumerable.Range(100, 7).Select(t => $"{t}:{onCooldown.Remaining(t)}"));
        Console.WriteLine($"                 Remaining, tick 100..106: {countdown}");

        bool atTheBoundary = onCooldown.TryUse(onCooldown.ReadyAtTick, out var reused);
        Console.WriteLine($"                 TryUse(ReadyAtTick)  {atTheBoundary}   (boundary inclusive) -> ReadyAtTick {reused.ReadyAtTick}");

        // A skipped stretch of ticks needs no replay: the state is two longs, so one query answers it.
        Console.WriteLine($"                 IsReady(1_000_000)   {reused.IsReady(1_000_000)}   (a catch-up gap costs one comparison)");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 17. default(TickCooldown) is legal and exactly equal to Create(0, 0) -- but, unlike
        //     default(Cooldown) (whose ReadyAt is DateTimeOffset.MinValue, so it is ready across the
        //     whole timeline), it is ready from tick 0 *onward only*: 0 is long's default without
        //     being its minimum. A cooldown ready across the entire tick domain is a construction, not
        //     a special member -- build it at the bottom of the range.
        // ---------------------------------------------------------------------------------------
        var fromDefault = default(TickCooldown);
        var alwaysReady = TickCooldown.Create(durationTicks: 5, asOfTick: long.MinValue);

        Console.WriteLine("[Default]        default(TickCooldown), and the negative tick domain");
        Console.WriteLine($"                 == Create(0, 0)      {fromDefault == TickCooldown.Create(0, 0)}");
        Console.WriteLine($"                 IsReady(0)           {fromDefault.IsReady(0)}   (ready from tick 0, inclusive)");
        Console.WriteLine($"                 IsReady(-1)          {fromDefault.IsReady(-1)}  (and NOT before it -- the one difference from default(Cooldown))");
        Console.WriteLine($"                 Create(5, MinValue)  IsReady(-1) {alwaysReady.IsReady(-1)}, IsReady(long.MinValue) {alwaysReady.IsReady(long.MinValue)}  (ready at every representable tick)");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 18. The two arithmetic sites dispose of an out-of-range result differently, and both are
        //     contracts. TryUse adds (checked) and throws rather than wrapping a cooldown into the far
        //     past; Remaining subtracts and clamps into [0, long.MaxValue] rather than throwing, since
        //     a ReadyAtTick of long.MaxValue is a legal "effectively never ready" sentinel that a
        //     negative tick can be a wider-than-long distance away from.
        // ---------------------------------------------------------------------------------------
        var nearTheTop = TickCooldown.Create(durationTicks: 10, asOfTick: long.MaxValue - 5);
        var neverReady = new TickCooldown { DurationTicks = 0, ReadyAtTick = long.MaxValue };

        Console.WriteLine("[Overflow]       TryUse throws; Remaining clamps");

        try
        {
            _ = nearTheTop.TryUse(long.MaxValue, out _);
        }
        catch (OverflowException)
        {
            Console.WriteLine("                 TryUse(long.MaxValue) threw OverflowException  (never silently wrapped into the far past)");
        }

        Console.WriteLine($"                 Remaining(1)         {neverReady.Remaining(1)}  (exact: long.MaxValue - 1)");
        Console.WriteLine($"                 Remaining(0)         {neverReady.Remaining(0)}  (exact: the clamp boundary itself)");
        Console.WriteLine($"                 Remaining(-1)        {neverReady.Remaining(-1)}  (clamped, not wrapped -- a wrapped negative would read as 'ready')");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 19. One tick loop, both tick-axis types: TickSchedule answers *what is due* and TickCooldown
        //     answers *may this be used*. Neither knows about the other, and both use the same
        //     inclusive boundary -- so an event popped at tick N and the Reset it triggers make the
        //     ability usable at that very tick.
        // ---------------------------------------------------------------------------------------
        var skill = TickCooldown.Create(durationTicks: 3, asOfTick: 0);
        var schedule = TickSchedule<string>.Empty.Add("haste-buff", dueTick: 4);

        Console.WriteLine("[TickLoop]       a skill cooldown and an event schedule, driven by one counter");

        for (long tick = 0; tick <= 7; tick++)
        {
            foreach (var entry in schedule.PopDue(tick, out schedule))
            {
                skill = skill.Reset(tick);   // the buff clears the cooldown at the very tick it lands
                Console.WriteLine($"                 tick {tick}  {entry.Event} -> cooldown reset (usable at this same tick)");
            }

            if (skill.TryUse(tick, out var afterCast))
            {
                skill = afterCast;
                Console.WriteLine($"                 tick {tick}  cast          -> ReadyAtTick {skill.ReadyAtTick}");
            }
        }

        Console.WriteLine();
    }
}
