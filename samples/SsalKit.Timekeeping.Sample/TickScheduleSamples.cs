using System.Text.Json;
using SsalKit.Timekeeping;

// [TickSchedule] [Battle] [CatchUp] [Recurring] [SaveRestore]
internal static class TickScheduleSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 11. TickSchedule: a deterministic queue of events due at logical simulation ticks (a long the
        //     simulation itself counts), not wall-clock instants. Add appends -- dispatch order is never
        //     storage order -- and PopDue's boundary is inclusive: an event scheduled at tick 1800 is due
        //     starting at PopDue(1800), not only at PopDue(1801). Two events sharing a tick pop FIFO, in
        //     the order they were Add-ed.
        // ---------------------------------------------------------------------------------------
        var schedule = TickSchedule<string>.Empty
            .Add("boss-respawn", dueTick: 1800)
            .Add("wave-2", dueTick: 1800);

        Console.WriteLine("[TickSchedule]   two events due at the same tick, FIFO within the tie");
        Console.WriteLine($"                 Count {schedule.Count}  NextDueTick {schedule.NextDueTick}");

        var notYetDue = schedule.PopDue(1799, out var stillPending);
        Console.WriteLine($"                 PopDue(1799)         {notYetDue.Length} due  (the boundary has not arrived yet)");

        var due = stillPending.PopDue(1800, out var afterPop);
        Console.WriteLine($"                 PopDue(1800)         {due.Length} due, in order: {string.Join(" -> ", due.Select(e => e.Event))}  (boundary inclusive -- tick 1800 scheduled = tick 1800 executed)");
        Console.WriteLine($"                 afterPop.IsEmpty     {afterPop.IsEmpty}");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 12. Battle timeline: a poison tick and a one-shot buff expiry, interleaved and driven entirely
        //     by TickSchedule. The schedule only ever hands back the event value -- "poison-tick" -- never
        //     executes it, so what that value means to the battle stays ordinary application code.
        // ---------------------------------------------------------------------------------------
        var battle = TickSchedule<string>.Empty
            .Add("poison-tick", dueTick: 100)
            .Add("poison-tick", dueTick: 200)
            .Add("buff-expires", dueTick: 150);

        Console.WriteLine("[Battle]         a poison DoT and a buff expiry, interleaved by tick");
        var health = 100;
        for (long tick = 100; tick <= 200; tick += 50)
        {
            var dueNow = battle.PopDue(tick, out battle);
            foreach (var entry in dueNow)
            {
                switch (entry.Event)
                {
                    case "poison-tick":
                        health -= 10;
                        Console.WriteLine($"                 tick {entry.DueTick,4}  poison-tick   health -> {health}");
                        break;
                    case "buff-expires":
                        Console.WriteLine($"                 tick {entry.DueTick,4}  buff-expires  shield removed");
                        break;
                }
            }
        }

        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 13. Catch-up: a process that was offline can call PopDue once with the tick it caught up to,
        //     and every entry due at or before that tick comes back in one deterministic (DueTick,
        //     Sequence)-ordered batch -- there is no need to replay the ticks in between, which is what
        //     makes this the restart-recovery path.
        // ---------------------------------------------------------------------------------------
        var respawns = TickSchedule<string>.Empty
            .Add("goblin", dueTick: 500)
            .Add("orc", dueTick: 700)
            .Add("dragon", dueTick: 900);

        Console.WriteLine("[CatchUp]        a save was last flushed at tick 400; the process restarts at tick 1000");
        var missed = respawns.PopDue(1000, out var caughtUp);
        Console.WriteLine($"                 PopDue(1000) after a 600-tick gap: {missed.Length} entries, in order:");
        foreach (var entry in missed)
        {
            Console.WriteLine($"                   tick {entry.DueTick,4}  {entry.Event}");
        }

        Console.WriteLine($"                 caughtUp.IsEmpty     {caughtUp.IsEmpty}  (nothing left to catch up on)");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 14. Recurring events: v1 has no built-in repeat -- a wave spawner re-Adds itself for the next
        //     occurrence the moment the current one pops, which is the entire pattern in one line.
        // ---------------------------------------------------------------------------------------
        var waves = TickSchedule<string>.Empty.Add("wave", dueTick: 300);
        const long waveInterval = 300;

        Console.WriteLine("[Recurring]      a wave spawner re-Adds itself every 300 ticks");
        for (var i = 0; i < 3; i++)
        {
            var tick = waveInterval * (i + 1);
            var dueWaves = waves.PopDue(tick, out waves);
            foreach (var entry in dueWaves)
            {
                var nextTick = entry.DueTick + waveInterval;
                waves = waves.Add(entry.Event, nextTick);   // the re-Add: v1's whole recurring-event story
                Console.WriteLine($"                 tick {entry.DueTick,4}  {entry.Event} spawned  -> next wave scheduled for tick {nextTick}");
            }
        }

        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 15. Save/restore: Entries and NextSequence are the entire serialization surface. Round-tripping
        //     through System.Text.Json and popping the restored copy produces exactly the same order as
        //     popping the original -- storage order was never part of the determinism contract to begin
        //     with, so a deserializer is free to land entries in any order.
        // ---------------------------------------------------------------------------------------
        var original = TickSchedule<string>.Empty
            .Add("guard-patrol", dueTick: 50)
            .Add("gate-opens", dueTick: 50)
            .Add("alarm-resets", dueTick: 80);

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<TickSchedule<string>>(json);

        var fromOriginal = original.PopDue(100, out _);
        var fromRestored = restored.PopDue(100, out _);

        Console.WriteLine("[SaveRestore]    round-trip through JSON, then compare pop order");
        Console.WriteLine($"                 original PopDue(100): {string.Join(" -> ", fromOriginal.Select(e => e.Event))}");
        Console.WriteLine($"                 restored PopDue(100): {string.Join(" -> ", fromRestored.Select(e => e.Event))}");
        Console.WriteLine($"                 same order            {fromOriginal.SequenceEqual(fromRestored)}");
        Console.WriteLine();
    }
}
