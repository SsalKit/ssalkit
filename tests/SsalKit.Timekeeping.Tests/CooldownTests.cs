using System.Text.Json;

namespace SsalKit.Timekeeping.Tests;

public sealed class CooldownTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    // ---- Create ----

    [Fact]
    public void Create_IsImmediatelyReady()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromSeconds(30), Now);

        Assert.Equal(TimeSpan.FromSeconds(30), cooldown.Duration);
        AssertTime.Exact(Now, cooldown.ReadyAt);
        Assert.True(cooldown.IsReady(Now));
    }

    [Fact]
    public void Create_Throws_WhenDurationIsNegative()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Cooldown.Create(TimeSpan.FromTicks(-1), Now));
        Assert.Equal("duration", exception.ParamName);
    }

    [Fact]
    public void Create_Allows_ZeroDuration()
    {
        var cooldown = Cooldown.Create(TimeSpan.Zero, Now);

        Assert.Equal(TimeSpan.Zero, cooldown.Duration);
        Assert.True(cooldown.IsReady(Now));
        Assert.False(cooldown.IsReady(Now.AddDays(-1000)));
    }

    [Fact]
    public void Create_WithZeroDuration_IsAlwaysReadyAfterUse()
    {
        var cooldown = Cooldown.Create(TimeSpan.Zero, Now);

        Assert.True(cooldown.TryUse(Now, out var updated));
        Assert.True(updated.IsReady(Now));
        Assert.Equal(TimeSpan.Zero, updated.Remaining(Now));
    }

    // ---- Boundary precision (inclusive at ReadyAt, one tick either side) ----

    [Fact]
    public void IsReady_IsTrueExactlyAtReadyAt()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(1), Now);
        var readyAt = cooldown.ReadyAt + cooldown.Duration;

        Assert.True(cooldown.TryUse(Now, out var used));
        Assert.False(used.IsReady(readyAt.AddTicks(-1)));
        Assert.True(used.IsReady(readyAt));
        Assert.True(used.IsReady(readyAt.AddTicks(1)));
    }

    [Fact]
    public void Remaining_IsZeroExactlyAtReadyAt_AndPositiveOneTickBefore()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(1), Now);

        Assert.Equal(TimeSpan.FromTicks(1), cooldown.Remaining(cooldown.ReadyAt.AddTicks(-1)));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(cooldown.ReadyAt));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(cooldown.ReadyAt.AddTicks(1)));
    }

    [Fact]
    public void Remaining_NeverGoesNegative_LongAfterReady()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(1), Now);

        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now.AddDays(3650)));
    }

    // ---- TryUse / Reset ----

    [Fact]
    public void TryUse_Fails_WhenNotReady_AndLeavesStateUnchanged()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(1), Now);
        Assert.True(cooldown.TryUse(Now, out var afterFirstUse));

        var succeeded = afterFirstUse.TryUse(Now.AddSeconds(1), out var updated);

        Assert.False(succeeded);
        Assert.Equal(afterFirstUse, updated);
    }

    [Fact]
    public void TryUse_Succeeds_WhenReady_AndAdvancesReadyAtByDuration()
    {
        var duration = TimeSpan.FromMinutes(5);
        var cooldown = Cooldown.Create(duration, Now);

        var succeeded = cooldown.TryUse(Now, out var updated);

        Assert.True(succeeded);
        AssertTime.Exact(Now + duration, updated.ReadyAt);
        Assert.Equal(duration, updated.Duration);
    }

    [Fact]
    public void TryUse_Succeeds_ExactlyAtReadyAt()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now);
        Assert.True(cooldown.TryUse(Now, out var afterFirstUse));

        Assert.True(afterFirstUse.TryUse(afterFirstUse.ReadyAt, out _));
    }

    [Fact]
    public void Reset_MakesTheCooldownImmediatelyReady()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now);
        Assert.True(cooldown.TryUse(Now, out var onCooldown));
        Assert.False(onCooldown.IsReady(Now.AddMinutes(1)));

        var reset = onCooldown.Reset(Now.AddMinutes(1));

        AssertTime.Exact(Now.AddMinutes(1), reset.ReadyAt);
        Assert.True(reset.IsReady(Now.AddMinutes(1)));
        Assert.Equal(onCooldown.Duration, reset.Duration);
    }

    // ---- Time reversal: total, deterministic, never throws ----

    [Fact]
    public void IsReadyAndRemaining_AreTotal_ForAnyInstant_IncludingBeforeCreation()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now);
        var wayBefore = Now.AddYears(-100);

        Assert.False(cooldown.IsReady(wayBefore));
        Assert.True(cooldown.Remaining(wayBefore) > TimeSpan.Zero);
    }

    [Fact]
    public void TryUse_AtAnEarlierInstantThanAPriorUse_IsStillDecidedConsistently()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        // A clock regression does not throw; it is simply evaluated as "not ready yet" honestly.
        var succeeded = used.TryUse(Now.AddSeconds(-30), out var afterRegression);

        Assert.False(succeeded);
        Assert.Equal(used, afterRegression);
    }

    // ---- default(Cooldown): legal, always-ready, no guard needed ----

    [Fact]
    public void Default_IsLegal_AndAlwaysReady()
    {
        var cooldown = default(Cooldown);

        Assert.Equal(TimeSpan.Zero, cooldown.Duration);
        AssertTime.Exact(DateTimeOffset.MinValue, cooldown.ReadyAt);
        Assert.True(cooldown.IsReady(Now));
        Assert.True(cooldown.IsReady(DateTimeOffset.MinValue));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now));
    }

    [Fact]
    public void Default_BehavesLikeCreateWithZeroDurationAtMinValue()
    {
        var fromDefault = default(Cooldown);
        var fromCreate = Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue);

        Assert.Equal(fromCreate, fromDefault);
    }

    [Fact]
    public void Default_TryUse_Succeeds()
    {
        var cooldown = default(Cooldown);

        var succeeded = cooldown.TryUse(Now, out var updated);

        Assert.True(succeeded);
        AssertTime.Exact(Now, updated.ReadyAt);
    }

    // ---- A negative Duration is a genuinely invalid state, guarded everywhere ----
    //
    // Duration is a public init property, so a negative value cannot come from Create but can come
    // from an object initializer or from deserializing a corrupted payload. Left unguarded, a
    // negative Duration would let TryUse succeed while pushing ReadyAt *backwards*, silently
    // defeating the cooldown -- the review-reported reproduction below.

    [Fact]
    public void NegativeDuration_TryUse_Throws()
    {
        // The reviewer's exact reproduction: a Duration of -1 minute set via the init accessor,
        // bypassing Create's ArgumentOutOfRangeException guard entirely.
        var corrupted = new Cooldown { Duration = TimeSpan.FromMinutes(-1), ReadyAt = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.TryUse(Now, out _));
    }

    [Fact]
    public void NegativeDuration_IsReady_Throws()
    {
        var corrupted = new Cooldown { Duration = TimeSpan.FromMinutes(-1), ReadyAt = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.IsReady(Now));
    }

    [Fact]
    public void NegativeDuration_Remaining_Throws()
    {
        var corrupted = new Cooldown { Duration = TimeSpan.FromMinutes(-1), ReadyAt = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.Remaining(Now));
    }

    [Fact]
    public void NegativeDuration_Reset_Throws()
    {
        var corrupted = new Cooldown { Duration = TimeSpan.FromMinutes(-1), ReadyAt = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.Reset(Now));
    }

    [Fact]
    public void SystemTextJson_DeserializedNegativeDuration_IsGuardedTheSameAsAHandConstructedOne()
    {
        var corrupted = JsonSerializer.Deserialize<Cooldown>(
            """{"Duration":"-00:01:00","ReadyAt":"2026-07-25T12:00:00+00:00"}""");

        Assert.Equal(TimeSpan.FromMinutes(-1), corrupted.Duration);
        Assert.Throws<InvalidOperationException>(() => corrupted.IsReady(Now));
        Assert.Throws<InvalidOperationException>(() => corrupted.Remaining(Now));
        Assert.Throws<InvalidOperationException>(() => corrupted.TryUse(Now, out _));
        Assert.Throws<InvalidOperationException>(() => corrupted.Reset(Now));
    }

    [Fact]
    public void ZeroDuration_IsStillLegal_AfterTheNegativeDurationGuardWasAdded()
    {
        var cooldown = new Cooldown { Duration = TimeSpan.Zero, ReadyAt = Now };

        Assert.True(cooldown.IsReady(Now));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now));
        Assert.True(cooldown.TryUse(Now, out _));
        Assert.Equal(Now, cooldown.Reset(Now).ReadyAt);
    }

    [Fact]
    public void DefaultCooldown_IsStillLegal_AfterTheNegativeDurationGuardWasAdded()
    {
        var cooldown = default(Cooldown);

        Assert.True(cooldown.IsReady(Now));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now));
        Assert.True(cooldown.TryUse(Now, out _));
        AssertTime.Exact(Now, cooldown.Reset(Now).ReadyAt);
    }

    // ---- MinValue/MaxValue: BCL exceptions propagate ----

    [Fact]
    public void TryUse_Throws_WhenReadyAtWouldOverflowMaxValue()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromDays(1), DateTimeOffset.MaxValue.AddDays(-0.5));

        Assert.Throws<ArgumentOutOfRangeException>(() => cooldown.TryUse(cooldown.ReadyAt, out _));
    }

    [Fact]
    public void Create_AllowsAsOfAtTheExtremes_WithZeroDuration()
    {
        // Creation itself never adds to asOf, so MinValue/MaxValue asOf values are legal to create
        // with; the overflow surfaces later, from TryUse, as covered above.
        var atMax = Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MaxValue);
        var atMin = Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue);

        Assert.True(atMax.IsReady(DateTimeOffset.MaxValue));
        Assert.True(atMin.IsReady(DateTimeOffset.MinValue));
    }

    // ---- Offset invariance: same instant, different offset notation, same result ----

    [Fact]
    public void Operations_AreOffsetInvariant()
    {
        var readyAtUtc = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var readyAtSeoul = readyAtUtc.ToOffset(TimeSpan.FromHours(9));
        var asOfUtc = readyAtUtc.AddMinutes(-15);
        var asOfSeoul = asOfUtc.ToOffset(TimeSpan.FromHours(-4));

        var cooldownUtc = new Cooldown { Duration = TimeSpan.FromMinutes(30), ReadyAt = readyAtUtc };
        var cooldownSeoul = new Cooldown { Duration = TimeSpan.FromMinutes(30), ReadyAt = readyAtSeoul };

        Assert.Equal(cooldownUtc.IsReady(asOfUtc), cooldownSeoul.IsReady(asOfSeoul));
        Assert.Equal(cooldownUtc.Remaining(asOfUtc), cooldownSeoul.Remaining(asOfSeoul));
        Assert.Equal(cooldownUtc, cooldownSeoul);
    }

    // ---- System.Text.Json round-trip ----

    [Fact]
    public void SystemTextJson_RoundTrips_WithNoConverter()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(7), Now);

        var json = JsonSerializer.Serialize(cooldown);
        var roundTripped = JsonSerializer.Deserialize<Cooldown>(json);

        Assert.Equal(cooldown, roundTripped);
    }

    [Fact]
    public void SystemTextJson_RoundTrippedCooldown_BehavesIdentically()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(7), Now);
        Assert.True(cooldown.TryUse(Now, out var used));

        var json = JsonSerializer.Serialize(used);
        var roundTripped = JsonSerializer.Deserialize<Cooldown>(json);

        Assert.Equal(used.IsReady(Now.AddMinutes(3)), roundTripped.IsReady(Now.AddMinutes(3)));
        Assert.Equal(used.Remaining(Now.AddMinutes(3)), roundTripped.Remaining(Now.AddMinutes(3)));
    }
}
