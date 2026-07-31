using System.Collections.Immutable;
using System.Text.Json;

namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// System.Text.Json round trips of <see cref="TickSchedule{TEvent}"/> for both an enum and a record
/// <c>TEvent</c>, plus the two corruption scenarios the design calls out (design §4.7, §3.3, §3.4):
/// a payload with duplicate <see cref="TickScheduleEntry{TEvent}.Sequence"/> values injected via
/// <c>init</c>, and a <see cref="TickSchedule{TEvent}.NextSequence"/> regressed below an existing
/// entry's <see cref="TickScheduleEntry{TEvent}.Sequence"/>. Both must leave <c>PopDue</c>
/// deterministic thanks to the storage-position tie-break documented on the type.
/// </summary>
public sealed class TickScheduleSerializationTests
{
    private enum BattleEvent
    {
        PoisonTick,
        BuffExpires,
        BossRespawn,
    }

    private sealed record LootDrop(string ItemId, int Quantity);

    [Fact]
    public void EnumEvent_RoundTripsThroughSystemTextJson_WithIdenticalPopDueResult()
    {
        var schedule = TickSchedule<BattleEvent>.Empty
            .Add(BattleEvent.PoisonTick, 5)
            .Add(BattleEvent.BuffExpires, 10)
            .Add(BattleEvent.BossRespawn, 5);

        var json = JsonSerializer.Serialize(schedule);
        var restored = JsonSerializer.Deserialize<TickSchedule<BattleEvent>>(json);

        var expectedDue = schedule.PopDue(10, out _);
        var actualDue = restored.PopDue(10, out _);

        Assert.Equal(expectedDue.ToArray(), actualDue.ToArray());
        Assert.Equal(schedule.NextSequence, restored.NextSequence);
    }

    [Fact]
    public void RecordEvent_RoundTripsThroughSystemTextJson_WithIdenticalPopDueResult()
    {
        var schedule = TickSchedule<LootDrop>.Empty
            .Add(new LootDrop("gold", 100), 3)
            .Add(new LootDrop("potion", 1), 3)
            .Add(new LootDrop("sword", 1), 7);

        var json = JsonSerializer.Serialize(schedule);
        var restored = JsonSerializer.Deserialize<TickSchedule<LootDrop>>(json);

        var expectedDue = schedule.PopDue(7, out _);
        var actualDue = restored.PopDue(7, out _);

        Assert.Equal(expectedDue.ToArray(), actualDue.ToArray());
    }

    [Fact]
    public void EmptySchedule_RoundTripsThroughSystemTextJson()
    {
        var schedule = TickSchedule<BattleEvent>.Empty;

        var json = JsonSerializer.Serialize(schedule);
        var restored = JsonSerializer.Deserialize<TickSchedule<BattleEvent>>(json);

        Assert.True(restored.IsEmpty);
        Assert.Equal(0, restored.NextSequence);
    }

    // ---- Corruption resilience (design §3.3 third tie-break, §3.4 NextSequence regression) ----

    [Fact]
    public void DuplicateSequenceInjectedViaInit_StillProducesADeterministicPopDueOrder()
    {
        // Two entries share Sequence == 0 (impossible via Add, but reachable through init or a
        // corrupted deserialized payload). The storage-position tie-break must still give a total,
        // reproducible order: for entries tied on (DueTick, Sequence), the one stored first pops first.
        var corrupted = new TickSchedule<string>
        {
            Entries =
            [
                new TickScheduleEntry<string>(10, 0, "stored-first"),
                new TickScheduleEntry<string>(10, 0, "stored-second"),
            ],
            NextSequence = 1,
        };

        var firstPop = corrupted.PopDue(10, out _);
        var secondPop = corrupted.PopDue(10, out _); // same input -> pure function, same output

        Assert.Equal(["stored-first", "stored-second"], firstPop.Select(e => e.Event).ToArray());
        Assert.Equal(firstPop.ToArray(), secondPop.ToArray());
    }

    [Fact]
    public void DuplicateSequenceAcrossDifferentDueTicks_StillOrdersByDueTickFirst()
    {
        var corrupted = new TickSchedule<string>
        {
            Entries =
            [
                new TickScheduleEntry<string>(20, 5, "later-tick"),
                new TickScheduleEntry<string>(10, 5, "earlier-tick-same-sequence"),
            ],
            NextSequence = 6,
        };

        var due = corrupted.PopDue(20, out _);

        Assert.Equal(["earlier-tick-same-sequence", "later-tick"], due.Select(e => e.Event).ToArray());
    }

    [Fact]
    public void NextSequenceRegressedBelowAnExistingEntry_StillLeavesPopDueDeterministic()
    {
        var schedule = TickSchedule<string>.Empty.Add("a", 1).Add("b", 1).Add("c", 1);
        Assert.Equal(3, schedule.NextSequence);

        // Simulate a corrupted/hand-edited payload: NextSequence regressed to 1, so the next Add
        // collides with the existing entry "b" (Sequence == 1).
        var regressed = schedule with { NextSequence = 1 };
        var afterAdd = regressed.Add("d-collides-with-b", 1);

        Assert.Equal(2, afterAdd.Entries.Count(e => e.Sequence == 1));

        var due = afterAdd.PopDue(1, out _);

        // All four share DueTick == 1. "a" (Sequence 0) is unambiguously first. The Sequence-1 pair
        // ("b" and "d-collides-with-b") ties, so the storage-position tie-break decides between them:
        // "b" was stored before "d-collides-with-b" was appended, so it pops first. "c" (Sequence 2)
        // is unambiguously last.
        Assert.Equal(["a", "b", "d-collides-with-b", "c"], due.Select(e => e.Event).ToArray());

        // Determinism holds across repeated calls on the same (corrupted) value.
        var repeated = afterAdd.PopDue(1, out _);
        Assert.Equal(due.ToArray(), repeated.ToArray());
    }
}
