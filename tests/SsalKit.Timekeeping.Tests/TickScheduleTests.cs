using System.Collections.Immutable;

namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// Core behavior of <see cref="TickSchedule{TEvent}"/>: the single determinism rule
/// (<c>DueTick</c> ascending, then <c>Sequence</c> ascending), its independence from storage order,
/// inclusive-boundary catch-up, <see langword="default"/>/<see cref="TickSchedule{TEvent}.Empty"/>
/// totality, and <see cref="TickSchedule{TEvent}.RemoveAll"/>. See
/// <c>.design/SsalKit.Timekeeping.TickSchedule.design.md</c> §4 for the enumerated test strategy this
/// file (and its siblings) implements.
/// </summary>
public sealed class TickScheduleTests
{
    // ---- Determinism (design §4.1) ----

    [Fact]
    public void ReplayingTheSameAddPopSequence_ProducesIdenticalResults()
    {
        static ImmutableArray<TickScheduleEntry<string>> Run()
        {
            var schedule = TickSchedule<string>.Empty
                .Add("alpha", 10)
                .Add("beta", 5)
                .Add("gamma", 10)
                .Add("delta", 1);

            var firstPop = schedule.PopDue(5, out schedule);
            var secondPop = schedule.PopDue(10, out schedule);

            return firstPop.AddRange(secondPop);
        }

        var first = Run();
        var second = Run();

        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public void EntriesDueOnTheSameTick_PopInSequenceOrder_FirstInFirstOut()
    {
        var schedule = TickSchedule<string>.Empty
            .Add("first", 100)
            .Add("second", 100)
            .Add("third", 100);

        var due = schedule.PopDue(100, out _);

        Assert.Equal(["first", "second", "third"], due.Select(e => e.Event).ToArray());
        Assert.Equal([0L, 1L, 2L], due.Select(e => e.Sequence).ToArray());
    }

    // ---- Storage-order independence (design §4.2, §3.2) ----

    [Fact]
    public void PopDue_IsIndependentOfEntriesStorageOrder()
    {
        var entries = ImmutableArray.Create(
            new TickScheduleEntry<string>(10, 2, "c"),
            new TickScheduleEntry<string>(5, 0, "a"),
            new TickScheduleEntry<string>(10, 1, "b"),
            new TickScheduleEntry<string>(20, 3, "d"));

        var inOriginalOrder = new TickSchedule<string> { Entries = entries, NextSequence = 4 };
        var inReverseOrder = new TickSchedule<string> { Entries = [.. entries.Reverse()], NextSequence = 4 };
        var inShuffledOrder = new TickSchedule<string>
        {
            Entries = [entries[3], entries[0], entries[1], entries[2]],
            NextSequence = 4,
        };

        var dueFromOriginal = inOriginalOrder.PopDue(10, out _);
        var dueFromReverse = inReverseOrder.PopDue(10, out _);
        var dueFromShuffled = inShuffledOrder.PopDue(10, out _);

        Assert.Equal(["a", "b", "c"], dueFromOriginal.Select(e => e.Event).ToArray());
        Assert.Equal(dueFromOriginal.ToArray(), dueFromReverse.ToArray());
        Assert.Equal(dueFromOriginal.ToArray(), dueFromShuffled.ToArray());
    }

    // ---- Catch-up (design §4.3) ----

    [Fact]
    public void PopDue_AfterSkippingManyTicks_ReturnsEveryOverdueEntry_InDueTickThenSequenceOrder()
    {
        var schedule = TickSchedule<string>.Empty
            .Add("tick-50", 50)
            .Add("tick-10-a", 10)
            .Add("tick-30", 30)
            .Add("tick-10-b", 10)
            .Add("tick-1000", 1000); // still not due

        var due = schedule.PopDue(60, out var updated);

        Assert.Equal(["tick-10-a", "tick-10-b", "tick-30", "tick-50"], due.Select(e => e.Event).ToArray());
        Assert.Equal(1, updated.Count);
        Assert.Equal("tick-1000", updated.Entries[0].Event);
    }

    // ---- Boundary inclusion (design §4.4) ----

    [Fact]
    public void PopDue_IncludesAnEntryDueExactlyAtCurrentTick_ButNotOneDueOneTickLater()
    {
        var schedule = TickSchedule<string>.Empty.Add("due-now", 1800).Add("due-later", 1801);

        var due = schedule.PopDue(1800, out var updated);

        Assert.Equal(["due-now"], due.Select(e => e.Event).ToArray());
        Assert.Equal(1, updated.Count);
        Assert.Equal("due-later", updated.Entries[0].Event);
    }

    // ---- default / Empty totality (design §4.5) ----

    [Fact]
    public void DefaultSchedule_BehavesIdenticallyToEmpty()
    {
        var byDefault = default(TickSchedule<string>);
        var empty = TickSchedule<string>.Empty;

        Assert.Equal(0, byDefault.Count);
        Assert.Equal(0, empty.Count);
        Assert.True(byDefault.IsEmpty);
        Assert.True(empty.IsEmpty);
        Assert.Null(byDefault.NextDueTick);
        Assert.Null(empty.NextDueTick);
        Assert.Equal(0, byDefault.NextSequence);
        Assert.Equal(0, empty.NextSequence);

        Assert.Empty(byDefault.PopDue(long.MaxValue, out _));
        Assert.Empty(empty.PopDue(long.MaxValue, out _));
    }

    [Fact]
    public void DefaultSchedule_AcceptsAddJustLikeEmpty()
    {
        var fromDefault = default(TickSchedule<string>).Add("first", 1);
        var fromEmpty = TickSchedule<string>.Empty.Add("first", 1);

        Assert.Equal(1, fromDefault.Count);
        Assert.Equal(fromEmpty.Entries.ToArray(), fromDefault.Entries.ToArray());
        Assert.Equal(0, fromDefault.Entries[0].Sequence);
    }

    [Fact]
    public void PopDue_OnAnEmptySchedule_ReturnsEmptyArray_AndUpdatedIsExactlyThis()
    {
        var schedule = TickSchedule<string>.Empty;

        var due = schedule.PopDue(long.MaxValue, out var updated);

        Assert.Empty(due);
        Assert.Equal(schedule, updated);
    }

    [Fact]
    public void PopDue_WhenNothingIsCurrentlyDue_ReturnsUpdatedExactlyEqualToThis()
    {
        var schedule = TickSchedule<string>.Empty.Add("not-yet", 100);

        var due = schedule.PopDue(50, out var updated);

        Assert.Empty(due);
        Assert.Equal(schedule, updated);
        Assert.Equal(schedule.NextSequence, updated.NextSequence);
    }

    // ---- RemoveAll (design §4.6) ----

    [Fact]
    public void RemoveAll_RemovesEveryMatchingEntry_AndPreservesOrderOfTheRest()
    {
        var schedule = TickSchedule<string>.Empty
            .Add("keep-a", 1)
            .Add("target", 2)
            .Add("keep-b", 3)
            .Add("target", 4);

        var updated = schedule.RemoveAll("target");

        Assert.Equal(["keep-a", "keep-b"], updated.Entries.Select(e => e.Event).ToArray());
    }

    [Fact]
    public void RemoveAll_WhenNothingMatches_ReturnsThisUnchanged()
    {
        var schedule = TickSchedule<string>.Empty.Add("keep", 1);

        var updated = schedule.RemoveAll("absent");

        Assert.Equal(schedule, updated);
    }

    [Fact]
    public void RemoveAll_OnAnEmptySchedule_ReturnsThisUnchanged()
    {
        var schedule = TickSchedule<string>.Empty;

        var updated = schedule.RemoveAll("anything");

        Assert.Equal(schedule, updated);
    }

    [Fact]
    public void RemoveAll_EveryEntry_ResultsInAnEmptySchedule()
    {
        var schedule = TickSchedule<string>.Empty.Add("only", 1).Add("only", 2);

        var updated = schedule.RemoveAll("only");

        Assert.Equal(0, updated.Count);
        Assert.True(updated.IsEmpty);
    }

    [Fact]
    public void RemoveAll_ThenAdd_DoesNotReuseTheRemovedSequenceNumbers()
    {
        var schedule = TickSchedule<string>.Empty.Add("a", 1).Add("b", 2).Add("c", 3);
        Assert.Equal(3, schedule.NextSequence);

        var afterRemove = schedule.RemoveAll("b");
        Assert.Equal(3, afterRemove.NextSequence); // unchanged by RemoveAll

        var afterReAdd = afterRemove.Add("d", 4);

        Assert.Equal(3, afterReAdd.Entries[^1].Sequence);
        Assert.DoesNotContain(afterReAdd.Entries, e => e.Sequence is 1); // "b"'s old sequence is gone, not reused
    }

    // ---- Extreme / negative ticks, Count/IsEmpty/NextDueTick, checked Sequence (design §4.8) ----

    [Fact]
    public void NegativeAndExtremeDueTicks_AreLegal_AndCompareCorrectly()
    {
        var schedule = TickSchedule<string>.Empty
            .Add("very-negative", long.MinValue)
            .Add("negative", -5)
            .Add("zero", 0)
            .Add("very-positive", long.MaxValue);

        Assert.Equal(long.MinValue, schedule.NextDueTick);

        var due = schedule.PopDue(0, out var updated);

        Assert.Equal(["very-negative", "negative", "zero"], due.Select(e => e.Event).ToArray());
        Assert.Equal(1, updated.Count);
        Assert.Equal(long.MaxValue, updated.NextDueTick);
    }

    [Fact]
    public void AddingAPastDueTick_IsLegal_AndIsImmediatelyCollectableByPopDue()
    {
        var schedule = TickSchedule<string>.Empty.Add("already-late", -100);

        var due = schedule.PopDue(0, out var updated);

        Assert.Equal(["already-late"], due.Select(e => e.Event).ToArray());
        Assert.True(updated.IsEmpty);
    }

    [Fact]
    public void NextDueTick_Count_IsEmpty_TrackTheScheduleThroughAddAndPop()
    {
        var schedule = TickSchedule<string>.Empty;
        Assert.True(schedule.IsEmpty);
        Assert.Equal(0, schedule.Count);
        Assert.Null(schedule.NextDueTick);

        schedule = schedule.Add("later", 20).Add("earlier", 10);
        Assert.False(schedule.IsEmpty);
        Assert.Equal(2, schedule.Count);
        Assert.Equal(10, schedule.NextDueTick);

        schedule.PopDue(10, out schedule);
        Assert.Equal(1, schedule.Count);
        Assert.Equal(20, schedule.NextDueTick);

        schedule.PopDue(20, out schedule);
        Assert.True(schedule.IsEmpty);
        Assert.Null(schedule.NextDueTick);
    }

    [Fact]
    public void Add_UsesCheckedArithmeticForNextSequence_AndThrowsOverflowExceptionAtLongMaxValue()
    {
        var schedule = new TickSchedule<string> { Entries = [], NextSequence = long.MaxValue };

        Assert.Throws<OverflowException>(() => schedule.Add("one-too-many", 1));
    }

    [Fact]
    public void Add_AtNextSequenceLongMaxValueMinusOne_SucceedsAndReachesExactlyLongMaxValue()
    {
        var schedule = new TickSchedule<string> { Entries = [], NextSequence = long.MaxValue - 1 };

        var updated = schedule.Add("last-legal", 1);

        Assert.Equal(long.MaxValue - 1, updated.Entries[0].Sequence);
        Assert.Equal(long.MaxValue, updated.NextSequence);
    }
}
