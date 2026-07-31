using System.Text.Json;

namespace SsalKit.Timekeeping.Tests;

public sealed class RechargePoolTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Every = TimeSpan.FromMinutes(20);

    // ---- Create ----

    [Fact]
    public void Create_DefaultsToFull()
    {
        var pool = RechargePool.Create(5, Every, Now);

        Assert.Equal(5, pool.AvailableAt(Now));
        Assert.Null(pool.UntilFull(Now));
        Assert.Null(pool.UntilNextCharge(Now));
        AssertTime.Exact(Now, pool.FullAt);
    }

    [Fact]
    public void Create_WithExplicitInitialCharges_ReportsThatManyAvailable()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 2);

        Assert.Equal(2, pool.AvailableAt(Now));
    }

    [Fact]
    public void Create_WithZeroInitialCharges_IsEmpty()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 0);

        Assert.Equal(0, pool.AvailableAt(Now));
        Assert.NotNull(pool.UntilFull(Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_Throws_WhenCapacityIsLessThanOne(int capacity)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RechargePool.Create(capacity, Every, Now));
        Assert.Equal("capacity", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    public void Create_Throws_WhenRechargeEveryIsZero(int seconds)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RechargePool.Create(5, TimeSpan.FromSeconds(seconds), Now));
        Assert.Equal("rechargeEvery", exception.ParamName);
    }

    [Fact]
    public void Create_Throws_WhenRechargeEveryIsNegative()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RechargePool.Create(5, TimeSpan.FromSeconds(-1), Now));
        Assert.Equal("rechargeEvery", exception.ParamName);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(6)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Create_Throws_WhenInitialChargesIsOutOfRange(int initialCharges)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RechargePool.Create(5, Every, Now, initialCharges));
        Assert.Equal("initialCharges", exception.ParamName);
    }

    [Fact]
    public void Create_AllowsInitialChargesAtEachBoundOfTheRange()
    {
        Assert.Equal(0, RechargePool.Create(5, Every, Now, initialCharges: 0).AvailableAt(Now));
        Assert.Equal(5, RechargePool.Create(5, Every, Now, initialCharges: 5).AvailableAt(Now));
    }

    // ---- Boundary precision: available at FullAt is inclusive, one tick either side ----

    [Fact]
    public void AvailableAt_IsCapacityExactlyAtFullAt_AndOneTickAfter()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 0);

        Assert.Equal(2, pool.AvailableAt(pool.FullAt.AddTicks(-1)));
        Assert.Equal(3, pool.AvailableAt(pool.FullAt));
        Assert.Equal(3, pool.AvailableAt(pool.FullAt.AddTicks(1)));
    }

    [Fact]
    public void UntilFull_IsNullExactlyAtFullAt_AndPositiveOneTickBefore()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 0);

        Assert.Equal(TimeSpan.FromTicks(1), pool.UntilFull(pool.FullAt.AddTicks(-1)));
        Assert.Null(pool.UntilFull(pool.FullAt));
        Assert.Null(pool.UntilFull(pool.FullAt.AddTicks(1)));
    }

    [Fact]
    public void UntilNextCharge_IsNullWhenFull_AndPositiveWhenNot()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 3);
        Assert.Null(pool.UntilNextCharge(Now));

        Assert.True(pool.TryConsume(Now, 1, out var afterConsume));
        Assert.NotNull(afterConsume.UntilNextCharge(Now));
        Assert.Equal(Every, afterConsume.UntilNextCharge(Now));
    }

    [Fact]
    public void UntilNextCharge_TransitionsToNullExactlyAtTheChargeInstant()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 2);
        var nextChargeAt = Now + Every;

        Assert.Equal(TimeSpan.FromTicks(1), pool.UntilNextCharge(nextChargeAt.AddTicks(-1)));
        Assert.Null(pool.UntilNextCharge(nextChargeAt));
    }

    // ---- Algebra: available in [0, Capacity], monotonic non-decreasing in t ----

    [Fact]
    public void AvailableAt_IsAlwaysWithinZeroAndCapacity()
    {
        var pool = RechargePool.Create(4, Every, Now, initialCharges: 1);

        for (var minutes = -60; minutes <= 180; minutes += 5)
        {
            var available = pool.AvailableAt(Now.AddMinutes(minutes));
            Assert.InRange(available, 0, 4);
        }
    }

    [Fact]
    public void AvailableAt_IsMonotonicallyNonDecreasing_AsTimeAdvances()
    {
        var pool = RechargePool.Create(4, Every, Now, initialCharges: 0);

        var previous = pool.AvailableAt(Now.AddHours(-1));
        for (var minutes = -60; minutes <= 180; minutes += 1)
        {
            var current = pool.AvailableAt(Now.AddMinutes(minutes));
            Assert.True(current >= previous, $"available regressed at minute {minutes}: {previous} -> {current}");
            previous = current;
        }
    }

    [Fact]
    public void ConsumeThenGrant_RestoresTheOriginalFullAtExactly_WhenAChargeWasPending()
    {
        // Capacity == initialCharges, so the pool is exactly full at "Now": FullAt == Now, which
        // satisfies the lossless round-trip precondition FullAt >= asOf (the boundary-inclusive
        // "a charge was pending" case, since a unit becomes available *at* FullAt, not only after
        // it -- see the type's boundary semantics). See
        // ConsumeThenGrant_LandsOnTheConsumeInstant_RatherThanTheOriginalFullAt_WhenThePoolWasAlreadyFull
        // for the other branch of this contract, where the round trip is not exact.
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 5);
        Assert.True(pool.FullAt >= Now);

        Assert.True(pool.TryConsume(Now, 3, out var afterConsume));
        var restored = afterConsume.Grant(3, Now);

        AssertTime.Exact(pool.FullAt, restored.FullAt);
        Assert.Equal(pool, restored);
    }

    [Fact]
    public void GrantThenConsume_RestoresTheOriginalFullAtExactly_WhenAChargeWasPending()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 0);
        Assert.True(pool.FullAt >= Now); // not yet full: the lossless round-trip precondition holds.

        var granted = pool.Grant(2, Now);
        Assert.True(granted.TryConsume(Now, 2, out var restored));

        AssertTime.Exact(pool.FullAt, restored.FullAt);
        Assert.Equal(pool, restored);
    }

    [Fact]
    public void ConsumeThenGrant_LandsOnTheConsumeInstant_RatherThanTheOriginalFullAt_WhenThePoolWasAlreadyFull()
    {
        // The pool became full at "Now" but is queried (and consumed from) two hours later, when it
        // has long since been full: FullAt (Now) is strictly before the consume instant, so the
        // "charge was pending" precondition of the other round-trip tests does NOT hold.
        var pool = RechargePool.Create(4, Every, Now, initialCharges: 4);
        var consumeAt = Now.AddHours(2);
        Assert.True(pool.FullAt < consumeAt);

        Assert.True(pool.TryConsume(consumeAt, 1, out var afterConsume));
        var restored = afterConsume.Grant(1, consumeAt);

        // FullAt lands on the consume/grant instant itself, not on the pool's original FullAt.
        AssertTime.Exact(consumeAt, restored.FullAt);
        Assert.NotEqual(pool.FullAt, restored.FullAt);
        Assert.NotEqual(pool, restored);

        // But every observation made at or after that instant is identical: both states report the
        // pool completely full throughout, since both have FullAt <= any t >= consumeAt.
        foreach (var laterOffset in new[] { TimeSpan.Zero, TimeSpan.FromTicks(1), Every, TimeSpan.FromDays(400) })
        {
            var t = consumeAt + laterOffset;
            Assert.Equal(pool.AvailableAt(t), restored.AvailableAt(t));
            Assert.Equal(pool.UntilNextCharge(t), restored.UntilNextCharge(t));
            Assert.Equal(pool.UntilFull(t), restored.UntilFull(t));
        }

        // A query *before* the consume instant, however, tells them apart: the original pool was
        // already full there (FullAt == Now <= t < consumeAt), but the restored pool is not yet
        // (its FullAt == consumeAt > t).
        var beforeConsume = consumeAt.AddTicks(-1);
        Assert.Equal(4, pool.AvailableAt(beforeConsume));
        Assert.Equal(3, restored.AvailableAt(beforeConsume));
    }

    [Fact]
    public void TryConsume_Failure_LeavesStateCompletelyUnchanged()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 1);

        var succeeded = pool.TryConsume(Now, 2, out var updated);

        Assert.False(succeeded);
        Assert.Equal(pool, updated);
        Assert.Equal(pool.FullAt, updated.FullAt);
    }

    // ---- Partial progress toward the next unit is preserved exactly ----

    [Fact]
    public void ConsumingWhileFull_PreservesNoPriorProgress_AndPushesFullAtByExactlyOneInterval()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 3);

        Assert.True(pool.TryConsume(Now, 1, out var updated));

        AssertTime.Exact(Now + Every, updated.FullAt);
    }

    [Fact]
    public void ConsumingAtTheHalfwayPointOfAPendingCharge_PreservesTheProgressAlreadyMade()
    {
        // Pool with 1 of 2 capacity, so one unit is charging: FullAt = Now + Every.
        var pool = RechargePool.Create(2, Every, Now, initialCharges: 1);
        var halfway = Now + TimeSpan.FromTicks(Every.Ticks / 2);

        // Consuming the currently-available unit at the halfway point must not restart or lose the
        // progress already made toward the unit that is mid-recharge: FullAt is unaffected because
        // FullAt (Now + Every) is already later than "halfway", so consume's max(FullAt, t) picks
        // FullAt, and the result is FullAt + Every -- the pending charge's completion instant is
        // untouched relative to where it already was, and a second charge is queued behind it.
        Assert.True(pool.TryConsume(halfway, 1, out var updated));

        AssertTime.Exact(pool.FullAt + Every, updated.FullAt);

        // The originally-pending charge still completes at exactly the original instant.
        Assert.Equal(1, updated.AvailableAt(pool.FullAt));
    }

    [Fact]
    public void GrantingAtTheHalfwayPointOfAPendingCharge_KeepsProgressTowardTheFollowingUnit()
    {
        // Empty a 3-capacity pool down to 0, then observe partial progress toward the first unit.
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 0);
        var halfway = Now + TimeSpan.FromTicks(Every.Ticks / 2);

        // Grant one unit at the halfway point: FullAt' = max(halfway, FullAt - Every).
        var granted = pool.Grant(1, halfway);

        // FullAt was Now + 3*Every; subtracting one Every gives Now + 2*Every, still after halfway,
        // so the progress made toward that unit (half an interval) is preserved rather than reset.
        AssertTime.Exact(pool.FullAt - Every, granted.FullAt);
        Assert.Equal(1, granted.AvailableAt(halfway));
    }

    [Fact]
    public void Grant_NeverPushesFullAtBeforeAsOf_EvenWhenGrantingMoreThanMissing()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 2); // 1 missing

        var granted = pool.Grant(100, Now);

        AssertTime.Exact(Now, granted.FullAt);
        Assert.Equal(3, granted.AvailableAt(Now));
    }

    [Fact]
    public void Grant_OnAFullPool_KeepsItFull()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 3);

        var granted = pool.Grant(1, Now);

        Assert.Equal(3, granted.AvailableAt(Now));
    }

    // ---- Refill ----

    [Fact]
    public void Refill_MakesThePoolFullAtTheGivenInstant()
    {
        var pool = RechargePool.Create(4, Every, Now, initialCharges: 0);

        var refilled = pool.Refill(Now.AddHours(2));

        AssertTime.Exact(Now.AddHours(2), refilled.FullAt);
        Assert.Equal(4, refilled.AvailableAt(Now.AddHours(2)));

        // One tick before FullAt, exactly one unit (not the whole capacity) is still missing --
        // the ceiling of a sub-interval elapsed duration is 1, per the type-level formula.
        Assert.Equal(3, refilled.AvailableAt(Now.AddHours(2).AddTicks(-1)));

        // A whole capacity's worth of intervals before FullAt, the pool is completely empty.
        Assert.Equal(0, refilled.AvailableAt(Now.AddHours(2) - TimeSpan.FromTicks(Every.Ticks * 4)));
    }

    // ---- 10-year offline gap: O(1) correctness ----

    [Fact]
    public void AConsumedPool_IsFullAgainAfterTenYearsOffline_RegardlessOfIntervalLength()
    {
        var pool = RechargePool.Create(10, TimeSpan.FromMinutes(30), Now, initialCharges: 0);

        Assert.Equal(10, pool.AvailableAt(Now.AddYears(10)));
        Assert.Null(pool.UntilFull(Now.AddYears(10)));
    }

    [Fact]
    public void APartiallyChargedPool_ReportsExactAvailabilityAfterATenYearGap()
    {
        var pool = RechargePool.Create(5, TimeSpan.FromDays(1), Now, initialCharges: 0);

        // After exactly 3 days, exactly 3 units should be back (capped at capacity).
        Assert.Equal(3, pool.AvailableAt(Now.AddDays(3)));

        // After ten years (>> capacity * interval), it is simply full -- computed in O(1), not by
        // walking 3,653 days of recharges.
        Assert.Equal(5, pool.AvailableAt(Now.AddYears(10)));
    }

    // ---- Time reversal: total, deterministic, never throws ----

    [Fact]
    public void AvailableAt_IsTotal_ForAnyInstant_IncludingLongBeforeCreation()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 5);

        Assert.Equal(0, pool.AvailableAt(Now.AddYears(-100)));
    }

    [Fact]
    public void QueryingAtAnEarlierInstantThanAPriorQuery_IsDeterministic_AndNeverThrows()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 0);

        var later = pool.AvailableAt(Now.AddHours(2));
        var earlier = pool.AvailableAt(Now); // going "backwards" relative to the previous call

        Assert.Equal(0, earlier);
        Assert.True(later >= earlier);

        // Repeating the earlier query gives the same answer -- no hidden "last observed instant".
        Assert.Equal(earlier, pool.AvailableAt(Now));
    }

    [Fact]
    public void TryConsume_AtAnInstantBeforeThePoolWasLastObserved_JustReportsHonestly()
    {
        var pool = RechargePool.Create(3, Every, Now, initialCharges: 3);
        Assert.True(pool.TryConsume(Now, 3, out var emptied));

        // A clock regression relative to "Now" does not throw or corrupt state.
        var succeeded = emptied.TryConsume(Now.AddMinutes(-5), 1, out var updated);

        Assert.False(succeeded);
        Assert.Equal(emptied, updated);
    }

    // ---- Overflow-safe ceiling division in MissingCharges (review fix) ----
    //
    // The naive ceiling-division formula "(elapsed + RechargeEvery.Ticks - 1) / RechargeEvery.Ticks"
    // can overflow `long` when `elapsed` and `RechargeEvery.Ticks` are each individually well within
    // range but their *sum* is not -- even though the mathematically correct answer is small and
    // unremarkable. These tests pin the review-reported reproduction and its close relatives.
    //
    // Derivation for the repro pool below: Capacity=1, RechargeEvery=TimeSpan.MaxValue (~9.22e18
    // ticks), created full at DateTimeOffset.MaxValue, so FullAt == MaxValue and Create's own
    // FullAt arithmetic never has to add anything (missing = 0 there). Querying AvailableAt at
    // DateTimeOffset.MinValue makes elapsed = (FullAt - asOf).Ticks = (MaxValue - MinValue).Ticks,
    // the full DateTime tick range (~3.16e18) -- large, but strictly *less* than RechargeEvery.Ticks
    // (~9.22e18), because TimeSpan's range is wider than DateTime's. So
    // ceil(elapsed / RechargeEvery.Ticks) is exactly 1 (one incomplete recharge interval separates
    // MinValue from FullAt), clamped to Capacity (1) unchanged, giving AvailableAt = Capacity - 1 =
    // 0 -- not an exception.

    [Fact]
    public void AvailableAt_NeverOverflows_WhenElapsedAndRechargeEveryAreBothNearLongMaxValue()
    {
        var pool = RechargePool.Create(1, TimeSpan.MaxValue, DateTimeOffset.MaxValue);

        Assert.Equal(0, pool.AvailableAt(DateTimeOffset.MinValue));
    }

    [Fact]
    public void AvailableAt_AtFullAt_IsStillCapacity_WithAnExtremeRechargeEvery()
    {
        var pool = RechargePool.Create(1, TimeSpan.MaxValue, DateTimeOffset.MaxValue);

        Assert.Equal(1, pool.AvailableAt(pool.FullAt));
    }

    [Fact]
    public void UntilNextCharge_NeverOverflows_WithAnExtremeRechargeEveryAndAWideGap()
    {
        var pool = RechargePool.Create(1, TimeSpan.MaxValue, DateTimeOffset.MaxValue);

        // missing = 1 (derived above), so untilNext = (FullAt - 0 * RechargeEvery) - asOf.
        Assert.Equal(pool.FullAt - DateTimeOffset.MinValue, pool.UntilNextCharge(DateTimeOffset.MinValue));
    }

    [Fact]
    public void AvailableAt_IsMonotonicAndNeverThrows_AcrossTheFullRepresentableRangeWithAnExtremeRechargeEvery()
    {
        var pool = RechargePool.Create(1, TimeSpan.MaxValue, DateTimeOffset.MaxValue);

        DateTimeOffset[] probes =
        [
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue.AddYears(1000),
            new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero),
            DateTimeOffset.MaxValue.AddYears(-1000),
            DateTimeOffset.MaxValue,
        ];

        var previous = -1;
        foreach (var probe in probes)
        {
            var available = pool.AvailableAt(probe);
            Assert.InRange(available, 0, 1);
            Assert.True(available >= previous, $"available regressed at {probe:O}: {previous} -> {available}");
            previous = available;
        }

        // Only exactly at FullAt does the pool become available -- everywhere else in this range,
        // the single RechargeEvery interval (wider than the whole DateTime range) has not elapsed.
        Assert.Equal(1, previous);
    }

    // ---- Exception contract ----

    [Fact]
    public void TryConsume_Throws_WhenAmountIsLessThanOne()
    {
        var pool = RechargePool.Create(5, Every, Now);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.TryConsume(Now, 0, out _));
        Assert.Equal("amount", exception.ParamName);
    }

    [Fact]
    public void TryConsume_Throws_WhenAmountExceedsCapacity_EvenThoughItWouldNeverSucceed()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 0);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.TryConsume(Now, 6, out _));
        Assert.Equal("amount", exception.ParamName);
    }

    [Fact]
    public void TryConsume_ReturnsFalse_WhenAmountIsValidButCurrentlyUnavailable()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 2);

        var succeeded = pool.TryConsume(Now, 3, out var updated);

        Assert.False(succeeded);
        Assert.Equal(pool, updated);
    }

    [Fact]
    public void Grant_Throws_WhenAmountIsLessThanOne()
    {
        var pool = RechargePool.Create(5, Every, Now);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Grant(0, Now));
        Assert.Equal("amount", exception.ParamName);
    }

    // ---- default(RechargePool): a genuinely invalid state, guarded everywhere ----

    [Fact]
    public void Default_AvailableAt_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(RechargePool).AvailableAt(Now));
    }

    [Fact]
    public void Default_TryConsume_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(RechargePool).TryConsume(Now, 1, out _));
    }

    [Fact]
    public void Default_UntilNextCharge_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(RechargePool).UntilNextCharge(Now));
    }

    [Fact]
    public void Default_UntilFull_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(RechargePool).UntilFull(Now));
    }

    [Fact]
    public void Default_Grant_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(RechargePool).Grant(1, Now));
    }

    [Fact]
    public void Default_Refill_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(RechargePool).Refill(Now));
    }

    [Fact]
    public void ACorruptedPool_WithZeroCapacity_IsAlsoGuarded()
    {
        // Simulates a deserialized/corrupted payload that is not the literal `default` value but is
        // still an invalid state by the same criterion `default` fails on.
        var corrupted = new RechargePool { Capacity = 0, RechargeEvery = Every, FullAt = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.AvailableAt(Now));
    }

    [Fact]
    public void ACorruptedPool_WithNonPositiveRechargeEvery_IsAlsoGuarded()
    {
        var corrupted = new RechargePool { Capacity = 5, RechargeEvery = TimeSpan.Zero, FullAt = Now };

        Assert.Throws<InvalidOperationException>(() => corrupted.AvailableAt(Now));
    }

    // ---- MinValue/MaxValue: BCL exceptions propagate ----

    [Fact]
    public void Create_Throws_WhenInitialFullAtWouldOverflowMaxValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RechargePool.Create(
                5,
                TimeSpan.FromDays(1000),
                DateTimeOffset.MaxValue.AddDays(-1),
                initialCharges: 0));
    }

    [Fact]
    public void TryConsume_Throws_WhenTheResultingFullAtWouldOverflowMaxValue()
    {
        var pool = RechargePool.Create(2, TimeSpan.FromDays(1000), DateTimeOffset.MaxValue.AddDays(-1));

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.TryConsume(pool.FullAt, 1, out _));
    }

    [Fact]
    public void Grant_Throws_WhenTheCandidateFullAtWouldOverflowMinValue()
    {
        var pool = RechargePool.Create(2, TimeSpan.FromDays(1000), DateTimeOffset.MinValue.AddDays(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Grant(1, DateTimeOffset.MinValue));
    }

    [Fact]
    public void MultiplyingByAHugeAmount_ThrowsOverflow_RatherThanWrappingSilently()
    {
        var pool = RechargePool.Create(1, TimeSpan.FromDays(1), Now, initialCharges: 0);

        Assert.Throws<OverflowException>(() => pool.Grant(int.MaxValue, Now));
    }

    // ---- Offset invariance: same instant, different offset notation, same result ----

    [Fact]
    public void Operations_AreOffsetInvariant()
    {
        var fullAtUtc = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var fullAtSeoul = fullAtUtc.ToOffset(TimeSpan.FromHours(9));
        var asOfUtc = fullAtUtc.AddMinutes(-5);
        var asOfSeoul = asOfUtc.ToOffset(TimeSpan.FromHours(-4));

        var poolUtc = new RechargePool { Capacity = 4, RechargeEvery = Every, FullAt = fullAtUtc };
        var poolSeoul = new RechargePool { Capacity = 4, RechargeEvery = Every, FullAt = fullAtSeoul };

        Assert.Equal(poolUtc.AvailableAt(asOfUtc), poolSeoul.AvailableAt(asOfSeoul));
        Assert.Equal(poolUtc.UntilFull(asOfUtc), poolSeoul.UntilFull(asOfSeoul));
        Assert.Equal(poolUtc, poolSeoul);
    }

    // ---- System.Text.Json round-trip ----

    [Fact]
    public void SystemTextJson_RoundTrips_WithNoConverter()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 2);

        var json = JsonSerializer.Serialize(pool);
        var roundTripped = JsonSerializer.Deserialize<RechargePool>(json);

        Assert.Equal(pool, roundTripped);
    }

    [Fact]
    public void SystemTextJson_RoundTrippedPool_BehavesIdentically()
    {
        var pool = RechargePool.Create(5, Every, Now, initialCharges: 2);

        var json = JsonSerializer.Serialize(pool);
        var roundTripped = JsonSerializer.Deserialize<RechargePool>(json);

        Assert.Equal(pool.AvailableAt(Now.AddHours(1)), roundTripped.AvailableAt(Now.AddHours(1)));
        Assert.Equal(pool.UntilNextCharge(Now), roundTripped.UntilNextCharge(Now));
    }

    [Fact]
    public void SystemTextJson_DeserializedDefault_IsGuardedTheSameAsLiteralDefault()
    {
        // An empty JSON object deserializes every property to its default -- the "corrupted payload"
        // case the InvalidOperationException guard exists for.
        var corrupted = JsonSerializer.Deserialize<RechargePool>("{}");

        Assert.Throws<InvalidOperationException>(() => corrupted.AvailableAt(Now));
    }
}
