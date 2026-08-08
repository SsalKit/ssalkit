using System.Text.Json;

namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// Core behavior of <see cref="TickCooldown"/>: the inclusive boundary, totality under tick reversal,
/// the <see langword="default"/> contract (legal, ready from tick <c>0</c> onward but not before it),
/// the negative-<c>DurationTicks</c> guard, and the two arithmetic sites' opposite overflow
/// dispositions — <see cref="TickCooldown.TryUse"/> throws, <see cref="TickCooldown.Remaining"/>
/// clamps. See <c>.design/SsalKit.Timekeeping.design.md</c> §9 for the contracts this file pins.
/// </summary>
public sealed class TickCooldownTests
{
    private const long Now = 1_000;

    // ---- Create ----

    [Fact]
    public void Create_IsImmediatelyReady()
    {
        var cooldown = TickCooldown.Create(300, Now);

        Assert.Equal(300, cooldown.DurationTicks);
        Assert.Equal(Now, cooldown.ReadyAtTick);
        Assert.True(cooldown.IsReady(Now));
        Assert.Equal(0, cooldown.Remaining(Now));
    }

    [Fact]
    public void Create_Throws_WhenDurationIsNegative()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TickCooldown.Create(-1, Now));

        Assert.Equal("durationTicks", exception.ParamName);
    }

    [Fact]
    public void Create_Allows_ZeroDuration()
    {
        var cooldown = TickCooldown.Create(0, Now);

        Assert.Equal(0, cooldown.DurationTicks);
        Assert.True(cooldown.IsReady(Now));
        Assert.False(cooldown.IsReady(Now - 1));
    }

    [Fact]
    public void Create_WithZeroDuration_IsStillReadyAfterUse()
    {
        var cooldown = TickCooldown.Create(0, Now);

        Assert.True(cooldown.TryUse(Now, out var updated));
        Assert.Equal(Now, updated.ReadyAtTick);
        Assert.True(updated.IsReady(Now));
        Assert.Equal(0, updated.Remaining(Now));
    }

    [Fact]
    public void Create_PerformsNoArithmetic_SoExtremeTicksAreLegalToCreateWith()
    {
        // ReadyAtTick is an assignment, never asOfTick + DurationTicks -- so the extremes of the tick
        // domain are legal to create at; the overflow surfaces later, from TryUse.
        var atMax = TickCooldown.Create(long.MaxValue, long.MaxValue);
        var atMin = TickCooldown.Create(long.MaxValue, long.MinValue);

        Assert.Equal(long.MaxValue, atMax.ReadyAtTick);
        Assert.True(atMax.IsReady(long.MaxValue));
        Assert.Equal(long.MinValue, atMin.ReadyAtTick);
        Assert.True(atMin.IsReady(long.MinValue));
    }

    // ---- Boundary precision (inclusive at ReadyAtTick, one tick either side) ----

    [Fact]
    public void IsReady_IsTrueExactlyAtReadyAtTick()
    {
        var cooldown = TickCooldown.Create(60, Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        Assert.False(used.IsReady(used.ReadyAtTick - 1));
        Assert.True(used.IsReady(used.ReadyAtTick));
        Assert.True(used.IsReady(used.ReadyAtTick + 1));
    }

    [Fact]
    public void Remaining_IsZeroExactlyAtReadyAtTick_AndOneOneTickBefore()
    {
        var cooldown = TickCooldown.Create(60, Now);

        Assert.Equal(1, cooldown.Remaining(cooldown.ReadyAtTick - 1));
        Assert.Equal(0, cooldown.Remaining(cooldown.ReadyAtTick));
        Assert.Equal(0, cooldown.Remaining(cooldown.ReadyAtTick + 1));
    }

    [Fact]
    public void Remaining_CountsDownTickByTick_AcrossTheWholeWait()
    {
        var cooldown = TickCooldown.Create(5, Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        Assert.Equal([5L, 4L, 3L, 2L, 1L, 0L, 0L], Enumerable.Range(0, 7).Select(i => used.Remaining(Now + i)).ToArray());
    }

    [Fact]
    public void Remaining_NeverGoesNegative_LongAfterReady()
    {
        var cooldown = TickCooldown.Create(60, Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        Assert.Equal(0, used.Remaining(Now + 1_000_000));
    }

    [Fact]
    public void ALargeCatchUpGap_IsAnsweredInOneQuery_WithNoPerTickStepping()
    {
        // A simulation that skipped several thousand ticks (a restart, or a fast-forward) queries the
        // cooldown once at the tick it caught up to; state is (DurationTicks, ReadyAtTick) only, so
        // there is nothing to replay.
        var cooldown = TickCooldown.Create(1_200, Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        Assert.False(used.IsReady(Now + 1_199));
        Assert.True(used.IsReady(Now + 5_000));
        Assert.Equal(0, used.Remaining(Now + 5_000));
        Assert.True(used.TryUse(Now + 5_000, out var reused));
        Assert.Equal(Now + 6_200, reused.ReadyAtTick);
    }

    // ---- TryUse / Reset ----

    [Fact]
    public void TryUse_Succeeds_WhenReady_AndAdvancesReadyAtTickByDuration()
    {
        var cooldown = TickCooldown.Create(300, Now);

        var succeeded = cooldown.TryUse(Now, out var updated);

        Assert.True(succeeded);
        Assert.Equal(Now + 300, updated.ReadyAtTick);
        Assert.Equal(300, updated.DurationTicks);
    }

    [Fact]
    public void TryUse_Fails_WhenNotReady_AndLeavesStateUnchanged()
    {
        var cooldown = TickCooldown.Create(300, Now);
        Assert.True(cooldown.TryUse(Now, out var afterFirstUse));

        var succeeded = afterFirstUse.TryUse(Now + 1, out var updated);

        Assert.False(succeeded);
        Assert.Equal(afterFirstUse, updated);
    }

    [Fact]
    public void TryUse_Succeeds_ExactlyAtReadyAtTick()
    {
        var cooldown = TickCooldown.Create(300, Now);
        Assert.True(cooldown.TryUse(Now, out var afterFirstUse));

        Assert.True(afterFirstUse.TryUse(afterFirstUse.ReadyAtTick, out var updated));
        Assert.Equal(afterFirstUse.ReadyAtTick + 300, updated.ReadyAtTick);
    }

    [Fact]
    public void Reset_MakesTheCooldownImmediatelyReady_AndPreservesDuration()
    {
        var cooldown = TickCooldown.Create(300, Now);
        Assert.True(cooldown.TryUse(Now, out var onCooldown));
        Assert.False(onCooldown.IsReady(Now + 100));

        var reset = onCooldown.Reset(Now + 100);

        Assert.Equal(Now + 100, reset.ReadyAtTick);
        Assert.True(reset.IsReady(Now + 100));
        Assert.Equal(onCooldown.DurationTicks, reset.DurationTicks);
    }

    [Fact]
    public void Reset_ToAnEarlierTick_IsLegal_AndPerformsNoArithmetic()
    {
        var cooldown = TickCooldown.Create(300, Now);
        Assert.True(cooldown.TryUse(Now, out var onCooldown));

        var reset = onCooldown.Reset(long.MinValue);

        Assert.Equal(long.MinValue, reset.ReadyAtTick);
        Assert.True(reset.IsReady(long.MinValue));
    }

    // ---- Tick reversal: total, deterministic, never throws ----

    [Fact]
    public void IsReadyAndRemaining_AreTotal_ForAnyTick_IncludingBeforeCreation()
    {
        var cooldown = TickCooldown.Create(300, Now);
        var wayBefore = Now - 1_000_000;

        Assert.False(cooldown.IsReady(wayBefore));
        Assert.Equal(1_000_000, cooldown.Remaining(wayBefore));
    }

    [Fact]
    public void TryUse_AtAnEarlierTickThanAPriorUse_IsStillDecidedConsistently()
    {
        var cooldown = TickCooldown.Create(300, Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        // A tick regression does not throw; it is simply evaluated as "not ready yet" honestly.
        var succeeded = used.TryUse(Now - 30, out var afterRegression);

        Assert.False(succeeded);
        Assert.Equal(used, afterRegression);
    }

    [Fact]
    public void NegativeTicks_AreOrdinaryTickValues()
    {
        // Ticks are opaque: any long is legal, so a simulation numbering its ticks from a negative
        // origin behaves exactly like one starting at zero.
        var cooldown = TickCooldown.Create(50, -1_000);

        Assert.True(cooldown.TryUse(-1_000, out var used));
        Assert.Equal(-950, used.ReadyAtTick);
        Assert.False(used.IsReady(-951));
        Assert.True(used.IsReady(-950));
        Assert.Equal(1, used.Remaining(-951));
    }

    // ---- default(TickCooldown): legal, ready from tick 0 onward -- but not before it (design §9.4) ----

    [Fact]
    public void Default_EqualsCreateWithZeroDurationAtTickZero()
    {
        var fromDefault = default(TickCooldown);
        var fromCreate = TickCooldown.Create(0, 0);

        Assert.Equal(fromCreate, fromDefault);
        Assert.Equal(0, fromDefault.DurationTicks);
        Assert.Equal(0, fromDefault.ReadyAtTick);
    }

    [Fact]
    public void Default_IsReadyAtTickZero()
    {
        Assert.True(default(TickCooldown).IsReady(0));
        Assert.Equal(0, default(TickCooldown).Remaining(0));
    }

    [Fact]
    public void Default_IsNotReadyBeforeTickZero()
    {
        // The one place this type's default differs observably from default(Cooldown), whose ReadyAt
        // is DateTimeOffset.MinValue and therefore ready across the whole domain: 0 is long's default
        // without being its minimum. Pinned rather than papered over.
        Assert.False(default(TickCooldown).IsReady(-1));
        Assert.Equal(1, default(TickCooldown).Remaining(-1));
    }

    [Fact]
    public void CreateAtLongMinValue_IsReadyAcrossTheEntireTickDomain()
    {
        // The documented recipe for "always ready, whatever the tick origin": every representable
        // asOfTick is at or after long.MinValue, so the comparison alone gives the property.
        var alwaysReady = TickCooldown.Create(0, long.MinValue);

        Assert.True(alwaysReady.IsReady(long.MinValue));
        Assert.True(alwaysReady.IsReady(-1));
        Assert.True(alwaysReady.IsReady(0));
        Assert.True(alwaysReady.IsReady(long.MaxValue));
    }

    [Fact]
    public void Default_TryUseAndReset_Succeed()
    {
        var cooldown = default(TickCooldown);

        Assert.True(cooldown.TryUse(Now, out var updated));
        Assert.Equal(Now, updated.ReadyAtTick);
        Assert.Equal(Now, cooldown.Reset(Now).ReadyAtTick);
    }

    // ---- Overflow: checked on TryUse, clamped on Remaining (design §9.5) ----

    [Fact]
    public void TryUse_Throws_WhenReadyAtTickWouldOverflow()
    {
        var cooldown = TickCooldown.Create(1, 0);

        Assert.Throws<OverflowException>(() => cooldown.TryUse(long.MaxValue, out _));
    }

    [Fact]
    public void TryUse_ThatOverflows_LeavesTheCallersValueUntouched()
    {
        var cooldown = TickCooldown.Create(5, 0);
        var untouched = TickCooldown.Create(99, 42);

        Assert.Throws<OverflowException>(() => cooldown.TryUse(long.MaxValue, out untouched));

        // The out argument is assigned only on the success path, after the checked addition -- so a
        // caller reusing a variable across attempts keeps whatever it already held.
        Assert.Equal(TickCooldown.Create(99, 42), untouched);
    }

    [Fact]
    public void TryUse_WithZeroDuration_DoesNotOverflowAtTheTopOfTheRange()
    {
        var cooldown = TickCooldown.Create(0, 0);

        Assert.True(cooldown.TryUse(long.MaxValue, out var updated));
        Assert.Equal(long.MaxValue, updated.ReadyAtTick);
    }

    [Fact]
    public void Remaining_ClampsToLongMaxValue_WhenTheTrueDifferenceIsTooWide()
    {
        // ReadyAtTick == long.MaxValue is a legal "effectively never ready" sentinel; measured from a
        // negative tick, the true difference exceeds long.MaxValue and is clamped rather than wrapped
        // into a negative that would read as "ready".
        var neverReady = new TickCooldown { DurationTicks = 0, ReadyAtTick = long.MaxValue };

        Assert.Equal(long.MaxValue, neverReady.Remaining(-1));
        Assert.Equal(long.MaxValue, neverReady.Remaining(long.MinValue));
    }

    [Fact]
    public void Remaining_IsExact_AtAndBelowTheClampBoundary()
    {
        var neverReady = new TickCooldown { DurationTicks = 0, ReadyAtTick = long.MaxValue };

        Assert.Equal(long.MaxValue, neverReady.Remaining(0));          // the exact value is the boundary
        Assert.Equal(long.MaxValue - 1, neverReady.Remaining(1));      // exact, one below it
        Assert.Equal(0, neverReady.Remaining(long.MaxValue));          // inclusive boundary still holds
    }

    [Fact]
    public void Remaining_IsZeroForEveryTick_WhenReadyAtTickIsLongMinValue()
    {
        var alwaysReady = new TickCooldown { DurationTicks = 7, ReadyAtTick = long.MinValue };

        Assert.Equal(0, alwaysReady.Remaining(long.MinValue));
        Assert.Equal(0, alwaysReady.Remaining(0));
        Assert.Equal(0, alwaysReady.Remaining(long.MaxValue));
    }

    // ---- A negative DurationTicks is the one invalid state, guarded on every member ----
    //
    // DurationTicks is a public init property, so a negative value cannot come from Create but can
    // come from an object initializer or from deserializing a corrupted payload. Left unguarded, it
    // would let TryUse succeed while pushing ReadyAtTick *backwards*, silently defeating the cooldown.

    [Fact]
    public void NegativeDuration_IsReady_Throws()
    {
        var corrupted = new TickCooldown { DurationTicks = -1, ReadyAtTick = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.IsReady(Now));
    }

    [Fact]
    public void NegativeDuration_Remaining_Throws()
    {
        var corrupted = new TickCooldown { DurationTicks = -1, ReadyAtTick = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.Remaining(Now));
    }

    [Fact]
    public void NegativeDuration_TryUse_Throws()
    {
        var corrupted = new TickCooldown { DurationTicks = -1, ReadyAtTick = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.TryUse(Now, out _));
    }

    [Fact]
    public void NegativeDuration_Reset_Throws()
    {
        var corrupted = new TickCooldown { DurationTicks = -1, ReadyAtTick = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.Reset(Now));
    }

    [Fact]
    public void ZeroDuration_IsStillLegal_AfterTheNegativeDurationGuard()
    {
        var cooldown = new TickCooldown { DurationTicks = 0, ReadyAtTick = Now };

        Assert.True(cooldown.IsReady(Now));
        Assert.Equal(0, cooldown.Remaining(Now));
        Assert.True(cooldown.TryUse(Now, out _));
        Assert.Equal(Now, cooldown.Reset(Now).ReadyAtTick);
    }

    // ---- System.Text.Json round-trip ----

    [Fact]
    public void SystemTextJson_RoundTrips_WithNoConverter()
    {
        var cooldown = TickCooldown.Create(300, Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        var json = JsonSerializer.Serialize(used);
        var roundTripped = JsonSerializer.Deserialize<TickCooldown>(json);

        Assert.Equal(used, roundTripped);
        Assert.Equal(used.IsReady(Now + 150), roundTripped.IsReady(Now + 150));
        Assert.Equal(used.Remaining(Now + 150), roundTripped.Remaining(Now + 150));
    }

    [Fact]
    public void SystemTextJson_DeserializedNegativeDuration_IsGuardedTheSameAsAHandConstructedOne()
    {
        var corrupted = JsonSerializer.Deserialize<TickCooldown>(
            """{"DurationTicks":-1,"ReadyAtTick":1000}""");

        Assert.Equal(-1, corrupted.DurationTicks);
        Assert.Throws<InvalidOperationException>(() => corrupted.IsReady(Now));
        Assert.Throws<InvalidOperationException>(() => corrupted.Remaining(Now));
        Assert.Throws<InvalidOperationException>(() => corrupted.TryUse(Now, out _));
        Assert.Throws<InvalidOperationException>(() => corrupted.Reset(Now));
    }

    [Fact]
    public void SystemTextJson_RoundTripsTheDefaultValue()
    {
        var json = JsonSerializer.Serialize(default(TickCooldown));
        var roundTripped = JsonSerializer.Deserialize<TickCooldown>(json);

        Assert.Equal(default, roundTripped);
        Assert.True(roundTripped.IsReady(0));
        Assert.False(roundTripped.IsReady(-1));
    }
}
