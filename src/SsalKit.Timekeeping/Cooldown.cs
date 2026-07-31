namespace SsalKit.Timekeeping;

/// <summary>
/// A single elapsed-time cooldown — "this ability is usable again at this instant" — stored as the
/// instant it becomes ready rather than as a countdown, so it survives a process restart or an
/// offline gap without drifting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Boundary semantics.</b> A cooldown is usable <i>at</i> the instant it completes, not only
/// strictly after it: <see cref="IsReady(DateTimeOffset)"/> returns <see langword="true"/> at
/// <c>asOf == ReadyAt</c>, and <see cref="Remaining(DateTimeOffset)"/> is exactly
/// <see cref="TimeSpan.Zero"/> there. This is the same "the boundary belongs to the thing it opens"
/// convention as <see cref="RecurrenceSchedule"/>'s, applied to elapsed time instead of the calendar,
/// and it is a permanent, versioned contract: cooldown state is persisted, so a caller comparing a
/// stored <see cref="ReadyAt"/> against a freshly read clock needs the comparison to never change
/// meaning between releases.
/// </para>
/// <para>
/// <b>Everything is a pure function of (state, instant).</b> Nothing here reads the ambient clock;
/// the instant is always a parameter. For code that already holds an injected clock, the
/// <see cref="CooldownTimeProviderExtensions"/> overloads take a <see cref="TimeProvider"/> and
/// forward its <see cref="TimeProvider.GetUtcNow"/>.
/// </para>
/// <para>
/// <b>Comparisons are always by absolute instant, never by offset notation</b> — the same
/// <see cref="DateTimeOffset"/> convention used throughout this package. Two <see cref="ReadyAt"/>
/// values that denote the same instant under different UTC offsets compare, and behave, identically.
/// </para>
/// <para>
/// <b>Time never runs backwards on this type — every member is total.</b> There is no stored "last
/// observed instant" for a regression to violate: an <c>asOf</c> earlier than one
/// previously used is simply answered honestly by <see cref="IsReady"/> and <see cref="Remaining"/>,
/// and <see cref="TryUse(DateTimeOffset, out Cooldown)"/> fails (leaving the state unchanged) rather
/// than throwing when the cooldown is not ready yet.
/// </para>
/// <para>
/// <b><c>default(Cooldown)</c> is a legal, always-ready value</b>, not a corrupted one: its
/// <see cref="Duration"/> is <see cref="TimeSpan.Zero"/> (the legal degenerate "always ready"
/// duration — see <see cref="Duration"/>) and its <see cref="ReadyAt"/> equals
/// <see cref="DateTimeOffset.MinValue"/>, which every representable <c>asOf</c> is at or after.
/// Unlike <see cref="RechargePool"/>, whose default is a genuinely inert state that every member must
/// reject, no member of this type needs to guard against <c>default(Cooldown)</c> — it behaves
/// exactly like <c>Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue)</c>, a value a caller
/// could just as well construct on purpose. <b>A negative <see cref="Duration"/> is the actual
/// invalid state</b> — it cannot come from <see cref="Create"/>, but <see cref="Duration"/>'s
/// <see langword="init"/> accessor and deserialization can both produce it directly, and a negative
/// duration would let <see cref="TryUse(DateTimeOffset, out Cooldown)"/> push <see cref="ReadyAt"/>
/// <i>backwards</i> on a successful use, silently defeating the cooldown. Every member therefore
/// throws <see cref="InvalidOperationException"/> when <see cref="Duration"/> is negative, the same
/// as <see cref="RechargePool"/> guards its own invalid default.
/// </para>
/// <para>
/// <b>Range.</b> Arithmetic that would place <see cref="ReadyAt"/> or a comparison outside the range
/// of <see cref="DateTimeOffset"/> throws <see cref="ArgumentOutOfRangeException"/> from the
/// underlying BCL arithmetic, the same as elsewhere in this package.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var cooldown = Cooldown.Create(TimeSpan.FromSeconds(30), now);
/// // ... later ...
/// if (cooldown.TryUse(now, out var updated))
/// {
///     player.AbilityCooldown = updated;
/// }
/// </code>
/// </example>
public readonly record struct Cooldown
{
    /// <summary>
    /// Gets the length of time a successful <see cref="TryUse(DateTimeOffset, out Cooldown)"/> puts
    /// the cooldown into. <see cref="TimeSpan.Zero"/> is legal and produces a degenerate cooldown
    /// that is always ready — useful as the "no cooldown configured" case without a separate
    /// nullable wrapper.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the instant the cooldown becomes ready. The cooldown is usable at this instant and at
    /// every later one; see the boundary semantics documented on the type.
    /// </summary>
    public DateTimeOffset ReadyAt { get; init; }

    /// <summary>
    /// Creates a cooldown that is immediately usable and that, once used, takes
    /// <paramref name="duration"/> to become ready again.
    /// </summary>
    /// <param name="duration">The length of a cooldown period. Must not be negative;
    /// <see cref="TimeSpan.Zero"/> is legal and produces a cooldown that is always ready.</param>
    /// <param name="asOf">The instant of creation, and also the instant the cooldown is first ready
    /// at.</param>
    /// <returns>A cooldown with <see cref="ReadyAt"/> equal to <paramref name="asOf"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
    public static Cooldown Create(TimeSpan duration, DateTimeOffset asOf)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        return new Cooldown { Duration = duration, ReadyAt = asOf };
    }

    /// <summary>
    /// Determines whether the cooldown is usable at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">The instant to test.</param>
    /// <returns><see langword="true"/> if <paramref name="asOf"/> is at or after
    /// <see cref="ReadyAt"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Duration"/> is negative — a corrupted
    /// or hand-constructed invalid state.</exception>
    public bool IsReady(DateTimeOffset asOf)
    {
        EnsureValid();

        return asOf >= ReadyAt;
    }

    /// <summary>
    /// Returns how much longer the cooldown has left at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">The instant to measure from.</param>
    /// <returns><c>ReadyAt - asOf</c>, clamped to <see cref="TimeSpan.Zero"/> when the cooldown is
    /// already ready. Never negative.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Duration"/> is negative — a corrupted
    /// or hand-constructed invalid state.</exception>
    public TimeSpan Remaining(DateTimeOffset asOf)
    {
        EnsureValid();

        var remaining = ReadyAt - asOf;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Attempts to use the cooldown at <paramref name="asOf"/>, starting a fresh
    /// <see cref="Duration"/>-long wait when it succeeds.
    /// </summary>
    /// <param name="asOf">The instant of the attempted use.</param>
    /// <param name="updated">When this method returns <see langword="true"/>, the cooldown with
    /// <see cref="ReadyAt"/> advanced to <c>asOf + Duration</c>. When it returns
    /// <see langword="false"/>, this instance unchanged — a failed attempt never mutates state, so
    /// assigning <paramref name="updated"/> back over the original is always safe.</param>
    /// <returns><see langword="true"/> if the cooldown was ready and has now been used; otherwise,
    /// <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Duration"/> is negative — a corrupted
    /// or hand-constructed invalid state.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><c>asOf + Duration</c> falls outside the range
    /// of <see cref="DateTimeOffset"/>.</exception>
    public bool TryUse(DateTimeOffset asOf, out Cooldown updated)
    {
        EnsureValid();

        if (!IsReady(asOf))
        {
            updated = this;
            return false;
        }

        updated = this with { ReadyAt = asOf + Duration };
        return true;
    }

    /// <summary>
    /// Returns a cooldown that is immediately usable at <paramref name="asOf"/>, discarding any
    /// remaining wait.
    /// </summary>
    /// <param name="asOf">The instant the cooldown becomes ready at.</param>
    /// <returns>A cooldown with the same <see cref="Duration"/> and <see cref="ReadyAt"/> set to
    /// <paramref name="asOf"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Duration"/> is negative — a corrupted
    /// or hand-constructed invalid state.</exception>
    public Cooldown Reset(DateTimeOffset asOf)
    {
        EnsureValid();

        return this with { ReadyAt = asOf };
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <see cref="Duration"/> is negative — a
    /// state <see cref="Create"/> would never produce, but that the <see langword="init"/> accessor
    /// or deserialization of a corrupted payload can.
    /// </summary>
    private void EnsureValid()
    {
        if (Duration < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "This Cooldown has a negative Duration (a corrupted or hand-constructed invalid state); construct one with Cooldown.Create.");
        }
    }
}
