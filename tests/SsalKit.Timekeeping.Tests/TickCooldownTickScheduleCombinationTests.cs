namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// <see cref="TickCooldown"/> composed with <see cref="TickSchedule{TEvent}"/> in one tick loop
/// (design §9.6): the two answer different questions on the same tick axis -- the cooldown asks "may
/// this ability be used at this tick?" while the schedule asks "which events are due at it?" --
/// and neither knows about the other, so driving both from one loop counter is ordinary calling code.
/// </summary>
public sealed class TickCooldownTickScheduleCombinationTests
{
    private enum SimEvent
    {
        CastWindowOpens,
        EncounterEnds,
    }

    [Fact]
    public void OneTickLoop_DrivesASkillCooldownAndAnEventScheduleFromTheSameCounter()
    {
        // A skill on a 3-tick cooldown, and two scheduled events: a cast window that opens at tick 4
        // and the encounter ending at tick 9. The loop below is the only thing that knows about both.
        var skill = TickCooldown.Create(durationTicks: 3, asOfTick: 0);
        var schedule = TickSchedule<SimEvent>.Empty
            .Add(SimEvent.CastWindowOpens, dueTick: 4)
            .Add(SimEvent.EncounterEnds, dueTick: 9);

        var castTicks = new List<long>();
        var events = new List<(long Tick, SimEvent Event)>();
        var castingAllowed = true;

        for (long tick = 0; tick <= 10; tick++)
        {
            foreach (var entry in schedule.PopDue(tick, out schedule))
            {
                events.Add((tick, entry.Event));

                if (entry.Event is SimEvent.EncounterEnds)
                {
                    castingAllowed = false;
                }
            }

            if (castingAllowed && skill.TryUse(tick, out var used))
            {
                skill = used;
                castTicks.Add(tick);
            }
        }

        // Ready at tick 0, then every 3 ticks after each successful use -- and never after the
        // encounter-end event was popped at tick 9.
        Assert.Equal([0L, 3L, 6L], castTicks);
        Assert.Equal([(4L, SimEvent.CastWindowOpens), (9L, SimEvent.EncounterEnds)], events);
        Assert.True(schedule.IsEmpty);
        Assert.Equal(9, skill.ReadyAtTick);
    }

    [Fact]
    public void ACatchUpGap_LeavesTheCooldownReadyAndTheScheduleDrained_InASingleQueryEach()
    {
        // Both types answer a skipped stretch of ticks in one call: PopDue returns every entry due at
        // or before the caught-up tick, and the cooldown re-derives readiness from ReadyAtTick alone.
        var skill = TickCooldown.Create(durationTicks: 500, asOfTick: 100);
        Assert.True(skill.TryUse(100, out skill));

        var schedule = TickSchedule<SimEvent>.Empty
            .Add(SimEvent.CastWindowOpens, dueTick: 200)
            .Add(SimEvent.EncounterEnds, dueTick: 400);

        const long restartTick = 10_000;

        var missed = schedule.PopDue(restartTick, out schedule);

        Assert.Equal([SimEvent.CastWindowOpens, SimEvent.EncounterEnds], missed.Select(e => e.Event).ToArray());
        Assert.True(schedule.IsEmpty);
        Assert.True(skill.IsReady(restartTick));
        Assert.Equal(0, skill.Remaining(restartTick));
    }

    [Fact]
    public void AScheduledEventCanResetTheCooldown_AtTheExactTickItPops()
    {
        // The boundary-inclusive rule is the same on both types, so a "cooldown reset" event due at
        // tick N is popped by PopDue(N) and Reset makes the skill usable at that very tick.
        var skill = TickCooldown.Create(durationTicks: 100, asOfTick: 0);
        Assert.True(skill.TryUse(0, out skill));
        Assert.False(skill.IsReady(20));

        var schedule = TickSchedule<SimEvent>.Empty.Add(SimEvent.CastWindowOpens, dueTick: 20);

        var due = schedule.PopDue(20, out schedule);

        Assert.Single(due);
        skill = skill.Reset(due[0].DueTick);

        Assert.True(skill.IsReady(20));
        Assert.True(skill.TryUse(20, out skill));
        Assert.Equal(120, skill.ReadyAtTick);
    }
}
