namespace SsalKit.Timekeeping;

/// <summary>
/// A single cooldown measured in logical simulation ticks — "this ability is usable again at this
/// tick" — stored as the tick it becomes ready at rather than as a countdown, so it survives a
/// process restart or a skipped stretch of ticks without drifting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Boundary semantics.</b> A cooldown is usable <i>at</i> the tick it completes, not only strictly
/// after it: <see cref="IsReady(long)"/> returns <see langword="true"/> at
/// <c>asOfTick == ReadyAtTick</c>, and <see cref="Remaining(long)"/> is exactly <c>0</c> there. This
/// is the same "the boundary belongs to the thing it opens" convention used package-wide — by
/// <see cref="Cooldown"/> on the elapsed-time axis and by
/// <see cref="TickSchedule{TEvent}.PopDue(long, out TickSchedule{TEvent})"/> on this one — and it is a
/// permanent, versioned contract: cooldown state is persisted, so a caller comparing a stored
/// <see cref="ReadyAtTick"/> against the simulation's current tick needs the comparison to never
/// change meaning between releases.
/// </para>
/// <para>
/// <b>Everything is a pure function of (state, tick).</b> Nothing here reads a clock of any kind; the
/// tick is always a parameter. There is deliberately no <see cref="TimeProvider"/> sugar on this type,
/// the same as on <see cref="TickSchedule{TEvent}"/> and unlike
/// <see cref="CooldownTimeProviderExtensions"/> — a logical tick is not a wall-clock reading, so
/// forwarding <see cref="TimeProvider.GetUtcNow"/> would not produce a tick number at all. Advance
/// <c>asOfTick</c> from whatever the simulation already uses to count ticks.
/// </para>
/// <para>
/// <b>Ticks are opaque to this type.</b> Any <see langword="long"/> is a legal
/// <see cref="ReadyAtTick"/> or <c>asOfTick</c>, negative values included — this type only ever
/// compares ticks and adds <see cref="DurationTicks"/> to them, and never assigns them meaning. What
/// a tick is worth in wall-clock terms, and where a simulation's tick numbering starts, are the
/// caller's to decide; this type converts between neither.
/// </para>
/// <para>
/// <b>Ticks never run backwards on this type — every member is total.</b> There is no stored "last
/// observed tick" for a regression to violate: an <c>asOfTick</c> earlier than one previously used is
/// simply answered honestly by <see cref="IsReady"/> and <see cref="Remaining"/>, and
/// <see cref="TryUse(long, out TickCooldown)"/> fails (leaving the state unchanged) rather than
/// throwing when the cooldown is not ready yet.
/// </para>
/// <para>
/// <b><c>default(TickCooldown)</c> is a legal value</b>, not a corrupted one: its
/// <see cref="DurationTicks"/> is <c>0</c> (the legal degenerate "always ready" duration — see
/// <see cref="DurationTicks"/>) and its <see cref="ReadyAtTick"/> is <c>0</c>, so it behaves exactly
/// like <c>TickCooldown.Create(0, 0)</c> — a value a caller could just as well construct on purpose —
/// and no member needs to guard against it. Note what that does <i>not</i> say: unlike
/// <c>default(Cooldown)</c>, whose <see cref="Cooldown.ReadyAt"/> is
/// <see cref="DateTimeOffset.MinValue"/> and therefore ready across the whole timeline, this type's
/// default is ready from tick <c>0</c> onward (inclusive) and <b>not</b> ready at any negative tick,
/// because <c>0</c> is the default of <see langword="long"/> without being its minimum. A value that
/// is ready across the entire tick domain is constructible and needs no special support from this
/// type: <c>TickCooldown.Create(durationTicks, long.MinValue)</c>, which every representable
/// <c>asOfTick</c> is at or after. <b>A negative <see cref="DurationTicks"/> is the actual invalid
/// state</b> — it cannot come from <see cref="Create"/>, but <see cref="DurationTicks"/>'s
/// <see langword="init"/> accessor and deserialization can both produce it directly, and a negative
/// duration would let <see cref="TryUse(long, out TickCooldown)"/> push <see cref="ReadyAtTick"/>
/// <i>backwards</i> on a successful use, silently defeating the cooldown. Every member therefore
/// throws <see cref="InvalidOperationException"/> when <see cref="DurationTicks"/> is negative, the
/// same as <see cref="Cooldown"/> guards its own negative <see cref="Cooldown.Duration"/>.
/// </para>
/// <para>
/// <b>Range.</b> The two members that do arithmetic surface an out-of-range result differently, and
/// both behaviors are contracts. <see cref="TryUse(long, out TickCooldown)"/> computes
/// <c>asOfTick + DurationTicks</c> with checked arithmetic and throws
/// <see cref="OverflowException"/> rather than silently wrapping a cooldown into the far past — the
/// same treatment <see cref="TickSchedule{TEvent}.Add"/> gives its own <see langword="long"/> counter,
/// and nothing is mutated when it throws (this is an immutable value; the <c>updated</c> argument is
/// never assigned). <see cref="Remaining(long)"/>, by contrast, never throws for an out-of-range
/// result: a <see cref="ReadyAtTick"/> of <see cref="long.MaxValue"/> is a legal "effectively never
/// ready" sentinel, and measuring it from a negative <c>asOfTick</c> asks for a difference wider than
/// <see langword="long"/> can hold, so the true difference is clamped into
/// <c>[0, long.MaxValue]</c> — an honest direction and magnitude at the extremes, instead of a
/// wrapped negative that would read as "ready".
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var cooldown = TickCooldown.Create(durationTicks: 300, asOfTick: currentTick);
/// // ... the simulation advances ...
/// if (cooldown.TryUse(currentTick, out var updated))
/// {
///     player.DashCooldown = updated;
/// }
/// </code>
/// </example>
public readonly record struct TickCooldown
{
    /// <summary>
    /// Gets the number of ticks a successful <see cref="TryUse(long, out TickCooldown)"/> puts the
    /// cooldown into. <c>0</c> is legal and produces a degenerate cooldown that is ready at every tick
    /// at or after <see cref="ReadyAtTick"/> — useful as the "no cooldown configured" case without a
    /// separate nullable wrapper.
    /// </summary>
    public long DurationTicks { get; init; }

    /// <summary>
    /// Gets the tick the cooldown becomes ready at. The cooldown is usable at this tick and at every
    /// later one; see the boundary semantics documented on the type.
    /// </summary>
    public long ReadyAtTick { get; init; }

    /// <summary>
    /// Creates a cooldown that is immediately usable at <paramref name="asOfTick"/> and that, once
    /// used, takes <paramref name="durationTicks"/> ticks to become ready again.
    /// </summary>
    /// <param name="durationTicks">The length of a cooldown period, in ticks. Must not be negative;
    /// <c>0</c> is legal and produces a cooldown that is ready at every tick at or after
    /// <paramref name="asOfTick"/>.</param>
    /// <param name="asOfTick">The tick of creation, and also the tick the cooldown is first ready at.
    /// Any <see langword="long"/> is legal, negative values included; passing
    /// <see cref="long.MinValue"/> is the way to express "ready across the entire tick domain".</param>
    /// <returns>A cooldown with <see cref="ReadyAtTick"/> equal to <paramref name="asOfTick"/>. No
    /// arithmetic is performed, so no tick value can overflow here.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="durationTicks"/> is
    /// negative.</exception>
    public static TickCooldown Create(long durationTicks, long asOfTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationTicks);

        return new TickCooldown { DurationTicks = durationTicks, ReadyAtTick = asOfTick };
    }

    /// <summary>
    /// Determines whether the cooldown is usable at <paramref name="asOfTick"/>.
    /// </summary>
    /// <param name="asOfTick">The tick to test.</param>
    /// <returns><see langword="true"/> if <paramref name="asOfTick"/> is at or after
    /// <see cref="ReadyAtTick"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="DurationTicks"/> is negative — a
    /// corrupted or hand-constructed invalid state.</exception>
    public bool IsReady(long asOfTick)
    {
        EnsureValid();

        return asOfTick >= ReadyAtTick;
    }

    /// <summary>
    /// Returns how many more ticks the cooldown has left at <paramref name="asOfTick"/>.
    /// </summary>
    /// <param name="asOfTick">The tick to measure from.</param>
    /// <returns><c>ReadyAtTick - asOfTick</c>, clamped to <c>[0, long.MaxValue]</c>: <c>0</c> once the
    /// cooldown is ready, and <see cref="long.MaxValue"/> in the rare case where the true difference
    /// is too wide for a <see langword="long"/> (see the range note on the type). Never negative, and
    /// never throws for an extreme pair of tick values.</returns>
    /// <exception cref="InvalidOperationException"><see cref="DurationTicks"/> is negative — a
    /// corrupted or hand-constructed invalid state.</exception>
    public long Remaining(long asOfTick)
    {
        EnsureValid();

        if (asOfTick >= ReadyAtTick)
        {
            return 0;
        }

        // Not ready, so the true difference is in [1, 2^64). Reinterpreting the wrapped two's
        // complement subtraction as a ulong recovers that difference exactly -- including for pairs
        // whose difference does not fit in a long, which is precisely the case the clamp below exists
        // for. The comparison above runs first so this subtraction is only ever reached where the
        // result is meaningful, which also keeps a ReadyAtTick of long.MinValue from overflowing in
        // the opposite direction.
        var difference = unchecked((ulong)(ReadyAtTick - asOfTick));

        return difference > (ulong)long.MaxValue ? long.MaxValue : (long)difference;
    }

    /// <summary>
    /// Attempts to use the cooldown at <paramref name="asOfTick"/>, starting a fresh
    /// <see cref="DurationTicks"/>-long wait when it succeeds.
    /// </summary>
    /// <param name="asOfTick">The tick of the attempted use.</param>
    /// <param name="updated">When this method returns <see langword="true"/>, the cooldown with
    /// <see cref="ReadyAtTick"/> advanced to <c>asOfTick + DurationTicks</c>. When it returns
    /// <see langword="false"/>, this instance unchanged — a failed attempt never mutates state, so
    /// assigning <paramref name="updated"/> back over the original is always safe.</param>
    /// <returns><see langword="true"/> if the cooldown was ready and has now been used; otherwise,
    /// <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="DurationTicks"/> is negative — a
    /// corrupted or hand-constructed invalid state.</exception>
    /// <exception cref="OverflowException"><c>asOfTick + DurationTicks</c> is outside the range of
    /// <see langword="long"/>. Nothing is assigned to <paramref name="updated"/> in that case; the
    /// caller's own value is left untouched.</exception>
    public bool TryUse(long asOfTick, out TickCooldown updated)
    {
        EnsureValid();

        if (!IsReady(asOfTick))
        {
            updated = this;
            return false;
        }

        updated = this with { ReadyAtTick = checked(asOfTick + DurationTicks) };
        return true;
    }

    /// <summary>
    /// Returns a cooldown that is immediately usable at <paramref name="asOfTick"/>, discarding any
    /// remaining wait.
    /// </summary>
    /// <param name="asOfTick">The tick the cooldown becomes ready at.</param>
    /// <returns>A cooldown with the same <see cref="DurationTicks"/> and <see cref="ReadyAtTick"/> set
    /// to <paramref name="asOfTick"/>. No arithmetic is performed, so no tick value can overflow
    /// here.</returns>
    /// <exception cref="InvalidOperationException"><see cref="DurationTicks"/> is negative — a
    /// corrupted or hand-constructed invalid state.</exception>
    public TickCooldown Reset(long asOfTick)
    {
        EnsureValid();

        return this with { ReadyAtTick = asOfTick };
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <see cref="DurationTicks"/> is negative — a
    /// state <see cref="Create"/> would never produce, but that the <see langword="init"/> accessor or
    /// deserialization of a corrupted payload can.
    /// </summary>
    private void EnsureValid()
    {
        if (DurationTicks < 0)
        {
            throw new InvalidOperationException(
                "This TickCooldown has a negative DurationTicks (a corrupted or hand-constructed invalid state); construct one with TickCooldown.Create.");
        }
    }
}
