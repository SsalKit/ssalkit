namespace SsalKit.Timekeeping;

/// <summary>
/// A resource that recharges one unit at a time on a fixed interval up to a capacity — "stamina",
/// "ability charges", "login tokens" — represented as the single instant at which it becomes
/// completely full rather than as a per-unit timer list, so the whole pool is O(1) to query and to
/// update regardless of how many units are missing or how long the pool has been offline.
/// </summary>
/// <remarks>
/// <para>
/// <b>State is <see cref="FullAt"/>, a single instant.</b> Every other quantity — how many units are
/// available right now, how long until the next one, how long until the pool is completely full — is
/// derived from it and <see cref="RechargeEvery"/>:
/// </para>
/// <code>
/// available(t)  = Capacity - clamp(ceil((FullAt - t) / RechargeEvery), 0, Capacity)
/// consume(k, t) : FullAt' = max(FullAt, t) + k * RechargeEvery
/// grant(k, t)   : FullAt' = max(t, FullAt - k * RechargeEvery)
/// refill(t)     : FullAt' = t
/// </code>
/// <para>
/// This is a permanent, versioned contract, the same status as the daylight-saving rules on
/// <see cref="RecurrenceSchedule.PreviousBoundary(DateTimeOffset)"/>: <see cref="FullAt"/> is
/// persisted, so re-deriving <see cref="AvailableAt(DateTimeOffset)"/> from a stored value only
/// works if the formula never changes.
/// </para>
/// <para>
/// <b>Boundary semantics.</b> A unit is available at the instant it finishes recharging, not only
/// strictly after it — the same "the boundary belongs to the thing it opens" convention used
/// throughout this package. A pool whose <see cref="FullAt"/> equals <c>asOf</c> reports
/// <see cref="Capacity"/> available units, not <c>Capacity - 1</c>.
/// </para>
/// <para>
/// <b>Partial progress toward the next unit is preserved exactly</b> by consuming or granting
/// through <see cref="FullAt"/> rather than through a per-unit countdown: consuming a unit pushes
/// <see cref="FullAt"/> forward by one <see cref="RechargeEvery"/> from whichever is later of
/// <see cref="FullAt"/> and the consume instant, so it never resets progress toward a charge that
/// was already pending. Whether the matching <see cref="Grant"/> undoes that shift back to the
/// <i>original</i> <see cref="FullAt"/> depends on whether a charge was actually pending at the
/// moment of consumption:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>A charge was pending</b> (<see cref="FullAt"/> was at or after the consume instant): a
/// <see cref="TryConsume"/> immediately followed by the matching <see cref="Grant"/> (same amount,
/// same instant) restores the original <see cref="FullAt"/> exactly — the round trip is lossless,
/// including for observations made before the consume/grant instant.
/// </description></item>
/// <item><description>
/// <b>The pool was already full</b> (<see cref="FullAt"/> was at or before the consume instant): the
/// round trip instead lands <see cref="FullAt"/> on the consume/grant instant itself, not on
/// whatever earlier instant the pool had actually become full at. Every observation made <i>at or
/// after</i> that instant — <see cref="AvailableAt(DateTimeOffset)"/>,
/// <see cref="UntilNextCharge(DateTimeOffset)"/>, <see cref="UntilFull(DateTimeOffset)"/> — is still
/// identical to the original, since both states report the pool full throughout that range; only a
/// query <i>before</i> that instant, or comparing the two <see cref="RechargePool"/> values for
/// equality, can tell them apart.
/// </description></item>
/// </list>
/// <para>
/// <b>Comparisons are always by absolute instant, never by offset notation</b> — the same
/// <see cref="DateTimeOffset"/> convention used throughout this package. This type has no notion of
/// wall-clock time or calendar days; for a resource that resets on a calendar boundary ("daily
/// stamina reset at 04:00 local time"), combine <see cref="RecurrenceSchedule"/> with
/// <see cref="Refill"/> instead of trying to express the reset as a recharge rate.
/// </para>
/// <para>
/// <b>Time never runs backwards on this type — it is total.</b> There is no stored "last observed
/// instant" to violate: an <c>asOf</c> earlier than one previously used simply reports fewer (or
/// zero) available units, via the <c>clamp(..., 0, Capacity)</c> term above, never an exception and
/// never a corrupted state.
/// </para>
/// <para>
/// <b><c>default(RechargePool)</c> is not a legal value.</b> Its <see cref="Capacity"/> is
/// <c>0</c> and its <see cref="RechargeEvery"/> is <see cref="TimeSpan.Zero"/>, both of which
/// <see cref="Create"/> rejects — a pool that holds nothing and never recharges is not a degenerate
/// but still-usable state the way <see cref="Cooldown"/>'s default is; the formula above would
/// require dividing by a zero <see cref="RechargeEvery"/>. Every member therefore throws
/// <see cref="InvalidOperationException"/> on the default value, including one produced by
/// deserializing a corrupted or truncated payload.
/// </para>
/// <para>
/// <b>Range.</b> Arithmetic that overflows the tick range, or that would place <see cref="FullAt"/>
/// or a comparison outside the range of <see cref="DateTimeOffset"/>, throws
/// <see cref="ArgumentOutOfRangeException"/> or <see cref="OverflowException"/> from the underlying
/// checked arithmetic, the same as elsewhere in this package.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var pool = RechargePool.Create(capacity: 5, rechargeEvery: TimeSpan.FromMinutes(20), asOf: now);
///
/// if (pool.TryConsume(now, 1, out var updated))
/// {
///     player.Stamina = updated;
/// }
///
/// TimeSpan? untilNext = pool.UntilNextCharge(now);
/// </code>
/// </example>
public readonly record struct RechargePool
{
    /// <summary>
    /// Gets the maximum number of units the pool can hold. Always at least <c>1</c> for a value
    /// produced by <see cref="Create"/>.
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Gets the fixed interval it takes to recharge one unit. Always strictly positive for a value
    /// produced by <see cref="Create"/>.
    /// </summary>
    public TimeSpan RechargeEvery { get; init; }

    /// <summary>
    /// Gets the instant at which the pool becomes completely full — the single piece of state this
    /// type carries; see the type-level remarks for the formulas derived from it.
    /// </summary>
    public DateTimeOffset FullAt { get; init; }

    /// <summary>
    /// Creates a pool with the given capacity and recharge rate, starting with
    /// <paramref name="initialCharges"/> units already available.
    /// </summary>
    /// <param name="capacity">The maximum number of units the pool can hold. Must be at least
    /// <c>1</c>.</param>
    /// <param name="rechargeEvery">The fixed interval it takes to recharge one unit. Must be strictly
    /// positive.</param>
    /// <param name="asOf">The instant of creation.</param>
    /// <param name="initialCharges">The number of units available at <paramref name="asOf"/>. The
    /// default, <c>-1</c>, means "full" (equal to <paramref name="capacity"/>); any other value must
    /// be between <c>0</c> and <paramref name="capacity"/>, inclusive.</param>
    /// <returns>A pool whose <see cref="AvailableAt(DateTimeOffset)"/> at <paramref name="asOf"/>
    /// equals <paramref name="initialCharges"/> (or <paramref name="capacity"/>, when the default was
    /// used).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than
    /// <c>1</c>; <paramref name="rechargeEvery"/> is zero or negative; or
    /// <paramref name="initialCharges"/> is neither <c>-1</c> nor within
    /// <c>[0, capacity]</c>.</exception>
    public static RechargePool Create(
        int capacity,
        TimeSpan rechargeEvery,
        DateTimeOffset asOf,
        int initialCharges = -1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rechargeEvery, TimeSpan.Zero);

        var charges = initialCharges == -1 ? capacity : initialCharges;

        if (charges < 0 || charges > capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialCharges),
                initialCharges,
                $"initialCharges must be -1 (meaning full, i.e. {capacity}) or between 0 and {capacity}, inclusive.");
        }

        var missing = capacity - charges;
        var fullAt = asOf + MultiplyChecked(rechargeEvery, missing);

        return new RechargePool { Capacity = capacity, RechargeEvery = rechargeEvery, FullAt = fullAt };
    }

    /// <summary>
    /// Returns how many units are available at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">The instant to measure at.</param>
    /// <returns>A value between <c>0</c> and <see cref="Capacity"/>, inclusive.</returns>
    /// <exception cref="InvalidOperationException">This instance is <c>default(RechargePool)</c> or
    /// another invalid state.</exception>
    public int AvailableAt(DateTimeOffset asOf)
    {
        EnsureValid();

        return (int)(Capacity - MissingCharges(asOf));
    }

    /// <summary>
    /// Attempts to consume <paramref name="amount"/> units at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">The instant of the attempted consumption.</param>
    /// <param name="amount">The number of units to consume. Must be at least <c>1</c> and at most
    /// <see cref="Capacity"/>.</param>
    /// <param name="updated">When this method returns <see langword="true"/>, the pool with
    /// <see cref="FullAt"/> pushed forward by <c>amount * RechargeEvery</c> from whichever is later
    /// of <see cref="FullAt"/> and <paramref name="asOf"/> — which is what preserves any progress
    /// already made toward the next unit. When it returns <see langword="false"/>, this instance
    /// unchanged — a failed attempt never mutates state, so assigning <paramref name="updated"/> back
    /// over the original is always safe.</param>
    /// <returns><see langword="true"/> if at least <paramref name="amount"/> units were available and
    /// have now been consumed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This instance is <c>default(RechargePool)</c> or
    /// another invalid state.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is less than
    /// <c>1</c>, or greater than <see cref="Capacity"/> — a request that could never succeed against
    /// this pool, regardless of how long it is left to recharge, so it is rejected rather than
    /// silently returning <see langword="false"/> forever.</exception>
    public bool TryConsume(DateTimeOffset asOf, int amount, out RechargePool updated)
    {
        EnsureValid();
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);

        if (amount > Capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                $"amount ({amount}) exceeds Capacity ({Capacity}); a pool this size could never hold enough units to satisfy this request.");
        }

        if (AvailableAt(asOf) < amount)
        {
            updated = this;
            return false;
        }

        var baseInstant = FullAt > asOf ? FullAt : asOf;
        updated = this with { FullAt = baseInstant + MultiplyChecked(RechargeEvery, amount) };
        return true;
    }

    /// <summary>
    /// Returns how long until the next unit becomes available at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">The instant to measure from.</param>
    /// <returns><see langword="null"/> when the pool is already full at <paramref name="asOf"/>;
    /// otherwise, a strictly positive duration at most <see cref="RechargeEvery"/> long.</returns>
    /// <exception cref="InvalidOperationException">This instance is <c>default(RechargePool)</c> or
    /// another invalid state.</exception>
    public TimeSpan? UntilNextCharge(DateTimeOffset asOf)
    {
        EnsureValid();

        var missing = MissingCharges(asOf);

        if (missing == 0)
        {
            return null;
        }

        var nextChargeAt = FullAt - MultiplyChecked(RechargeEvery, (int)(missing - 1));
        return nextChargeAt - asOf;
    }

    /// <summary>
    /// Returns how long until the pool is completely full at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">The instant to measure from.</param>
    /// <returns><see langword="null"/> when the pool is already full at <paramref name="asOf"/>;
    /// otherwise, exactly <c>FullAt - asOf</c>.</returns>
    /// <exception cref="InvalidOperationException">This instance is <c>default(RechargePool)</c> or
    /// another invalid state.</exception>
    public TimeSpan? UntilFull(DateTimeOffset asOf)
    {
        EnsureValid();

        return FullAt > asOf ? FullAt - asOf : null;
    }

    /// <summary>
    /// Returns a pool with <paramref name="amount"/> units granted at <paramref name="asOf"/>,
    /// clamped so the pool never reports more than <see cref="Capacity"/> available units.
    /// </summary>
    /// <param name="amount">The number of units to grant. Must be at least <c>1</c>. Unlike
    /// <see cref="TryConsume"/>, there is no upper bound: granting more than
    /// <see cref="Capacity"/> is legal and simply saturates the pool at full.</param>
    /// <param name="asOf">The instant of the grant.</param>
    /// <returns>A pool with <see cref="FullAt"/> pulled backward by <c>amount * RechargeEvery</c>
    /// from its current value, but never before <paramref name="asOf"/> — which is what preserves any
    /// progress already made toward the next unit while never over-filling the pool.</returns>
    /// <exception cref="InvalidOperationException">This instance is <c>default(RechargePool)</c> or
    /// another invalid state.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is less than
    /// <c>1</c>.</exception>
    public RechargePool Grant(int amount, DateTimeOffset asOf)
    {
        EnsureValid();
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);

        var candidate = FullAt - MultiplyChecked(RechargeEvery, amount);
        var newFullAt = candidate > asOf ? candidate : asOf;
        return this with { FullAt = newFullAt };
    }

    /// <summary>
    /// Returns a pool that is completely full at <paramref name="asOf"/>, discarding any partial
    /// progress toward the next unit.
    /// </summary>
    /// <param name="asOf">The instant the pool becomes full at.</param>
    /// <returns>A pool with the same <see cref="Capacity"/> and <see cref="RechargeEvery"/>, and
    /// <see cref="FullAt"/> set to <paramref name="asOf"/>.</returns>
    /// <exception cref="InvalidOperationException">This instance is <c>default(RechargePool)</c> or
    /// another invalid state.</exception>
    /// <remarks>
    /// Useful for pairing with a calendar reset: refilling the pool whenever a
    /// <see cref="RecurrenceSchedule"/> boundary has been crossed (<see cref="RecurrenceSchedule.HasCrossed"/>)
    /// combines a wall-clock reset with an elapsed-time resource without either type needing to know
    /// about the other.
    /// </remarks>
    public RechargePool Refill(DateTimeOffset asOf)
    {
        EnsureValid();

        return this with { FullAt = asOf };
    }

    /// <summary>
    /// Returns the number of units still missing (not yet recharged) at <paramref name="asOf"/>,
    /// clamped to <c>[0, Capacity]</c> — the <c>clamp(ceil((FullAt - t) / RechargeEvery), 0,
    /// Capacity)</c> term of the type-level formula.
    /// </summary>
    private long MissingCharges(DateTimeOffset asOf)
    {
        var elapsed = (FullAt - asOf).Ticks;

        if (elapsed <= 0)
        {
            return 0;
        }

        checked
        {
            // ceil(elapsed / RechargeEvery.Ticks) == 1 + (elapsed - 1) / RechargeEvery.Ticks for
            // elapsed >= 1 (guaranteed by the guard above), by the standard integer-ceiling identity.
            // Unlike the more obvious "(elapsed + RechargeEvery.Ticks - 1) / RechargeEvery.Ticks",
            // this form never adds two values that can each be close to long.MaxValue, so it cannot
            // overflow where the equivalent sum would.
            var missing = 1 + ((elapsed - 1) / RechargeEvery.Ticks);
            return Math.Min(missing, Capacity);
        }
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when this instance is
    /// <c>default(RechargePool)</c> or another state <see cref="Create"/> would never produce.
    /// </summary>
    private void EnsureValid()
    {
        if (Capacity < 1 || RechargeEvery <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "This RechargePool is the default value (or a corrupted/truncated deserialized state); construct one with RechargePool.Create.");
        }
    }

    /// <summary>
    /// Multiplies a <see cref="TimeSpan"/> by an integer factor using checked arithmetic on its tick
    /// count, so that a tick-range overflow throws <see cref="OverflowException"/> rather than
    /// silently wrapping around to an unrelated duration.
    /// </summary>
    private static TimeSpan MultiplyChecked(TimeSpan span, int factor)
    {
        checked
        {
            return TimeSpan.FromTicks(span.Ticks * factor);
        }
    }
}
