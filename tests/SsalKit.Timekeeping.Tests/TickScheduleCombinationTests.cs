namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// <see cref="TickSchedule{TEvent}"/> composed with <see cref="RechargePool"/> in one scenario (design
/// §4.9): neither type knows about the other, but a tick-driven simulation loop can use the schedule
/// to decide <i>when</i> a recharge grant happens while the pool itself still owns <i>how much</i> is
/// available.
/// </summary>
public sealed class TickScheduleCombinationTests
{
    private enum SimEvent
    {
        RegenTick,
    }

    private static readonly DateTimeOffset Epoch = new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset InstantForTick(long tick) => Epoch + TimeSpan.FromSeconds(tick);

    [Fact]
    public void ATickLoop_CombinesRechargePoolGrantsWithScheduledRegenEvents_AcrossACatchUpGap()
    {
        // Five regen events, batch-scheduled ten ticks apart up front -- as opposed to the
        // pop-then-re-add "one live occurrence at a time" recurring pattern (covered separately
        // below), this is what lets a single PopDue catch up on several of them at once.
        var schedule = TickSchedule<SimEvent>.Empty;

        for (var tick = 10; tick <= 50; tick += 10)
        {
            schedule = schedule.Add(SimEvent.RegenTick, tick);
        }

        var pool = RechargePool.Create(
            capacity: 5,
            rechargeEvery: TimeSpan.FromMinutes(20),
            asOf: InstantForTick(0),
            initialCharges: 0);

        // The simulation loop only observes tick 25 next -- a single PopDue call must return every
        // regen event due by then (10 and 20), in dispatch order.
        var due = schedule.PopDue(25, out schedule);

        Assert.Equal([10, 20], due.Select(e => e.DueTick).ToArray());

        foreach (var entry in due)
        {
            pool = pool.Grant(1, InstantForTick(entry.DueTick));
        }

        Assert.Equal(2, pool.AvailableAt(InstantForTick(25)));

        // The simulation then goes offline for a long stretch (an offline/restart gap); one more
        // PopDue call catches up on the remaining three events (30, 40, 50) in the same call.
        var restartTick = 1000;
        var caughtUp = schedule.PopDue(restartTick, out schedule);

        Assert.Equal([30, 40, 50], caughtUp.Select(e => e.DueTick).ToArray());

        foreach (var entry in caughtUp)
        {
            pool = pool.Grant(1, InstantForTick(entry.DueTick));
        }

        // 2 (first batch) + 3 (caught up) = 5 grants against a capacity of 5 -> exactly full.
        Assert.Equal(5, pool.AvailableAt(InstantForTick(restartTick)));
        Assert.True(schedule.IsEmpty);
    }

    [Fact]
    public void APopThenReAddLoop_KeepsExactlyOneLiveRegenOccurrencePending_WhileGrantingThePool()
    {
        // The v1 "recurring reservation" pattern from the design (§ Non-goals): re-Add the same
        // event after popping it, instead of the library modeling recurrence itself. Unlike the
        // batch-scheduled test above, only one occurrence is ever pending, so catching up across a
        // gap requires the consumer to drive the loop repeatedly -- this test pins that shape.
        const int regenIntervalTicks = 10;

        var schedule = TickSchedule<SimEvent>.Empty.Add(SimEvent.RegenTick, regenIntervalTicks);
        var pool = RechargePool.Create(
            capacity: 5,
            rechargeEvery: TimeSpan.FromMinutes(20),
            asOf: InstantForTick(0),
            initialCharges: 0);

        for (var occurrence = 0; occurrence < 3; occurrence++)
        {
            var nextDueTick = schedule.NextDueTick!.Value;
            var due = schedule.PopDue(nextDueTick, out schedule);

            Assert.Single(due);

            pool = pool.Grant(1, InstantForTick(due[0].DueTick));
            schedule = schedule.Add(SimEvent.RegenTick, due[0].DueTick + regenIntervalTicks);

            // Exactly one occurrence is ever pending -- re-Add immediately replaces the one just popped.
            Assert.Equal(1, schedule.Count);
        }

        Assert.Equal(3, pool.AvailableAt(InstantForTick(regenIntervalTicks * 3)));
    }
}
