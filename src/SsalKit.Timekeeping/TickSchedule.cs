using System.Collections.Immutable;

namespace SsalKit.Timekeeping;

/// <summary>
/// A deterministic, serializable queue of events due at logical simulation ticks ("boss respawns at
/// tick 1800") rather than at wall-clock instants -- persist it, restore it, replay the same
/// <see cref="Add"/>/<see cref="PopDue"/> calls anywhere, and the dispatch order is bit-for-bit
/// identical every time.
/// </summary>
/// <typeparam name="TEvent">The event value type carried by every <see cref="TickScheduleEntry{TEvent}"/>
/// in this schedule. See the constraint guidance on <see cref="TickScheduleEntry{TEvent}"/>.</typeparam>
/// <remarks>
/// <para>
/// <b>Determinism -- one rule, permanent contract.</b> Dispatch order is
/// <c>(DueTick ascending, Sequence ascending)</c>, and nothing else, regardless of the order entries
/// happen to be stored in. <see cref="TickScheduleEntry{TEvent}.Sequence"/> is assigned by
/// <see cref="Add"/> in call order, so
/// entries sharing a <see cref="TickScheduleEntry{TEvent}.DueTick"/> pop first-in-first-out.
/// <see cref="PopDue"/> treats its boundary as inclusive: an entry due at tick 1800 is returned by
/// <c>PopDue(1800, ...)</c>, not only by <c>PopDue(1801, ...)</c> -- the same "the boundary belongs to
/// the thing it opens" convention used package-wide (see
/// <see cref="RecurrenceSchedule.PreviousBoundary(DateTimeOffset)"/>). Because this rule never
/// references storage order, the same sequence of <see cref="Add"/>/<see cref="PopDue"/> calls
/// produces the same observable results everywhere, forever -- across processes, across
/// serialization round trips, and across releases of this package.
/// </para>
/// <para>
/// <b>Storage is append-only; there is no sorted-order invariant to maintain or to violate.</b>
/// <see cref="Add"/> appends a new entry and never reorders <see cref="Entries"/>; all sorting happens
/// inside <see cref="PopDue"/>, at the moment it is needed. This is deliberate: a deserializer can
/// (and, from a corrupted or hand-edited payload, will) place <see cref="Entries"/> in an arbitrary
/// order via the <see langword="init"/> accessor, and a design that depended on that order staying
/// sorted would either have to re-validate it on every call or silently misbehave. A design with no
/// sorted-order invariant cannot be violated by any storage order, so every <see cref="PopDue"/> call
/// still honors the rule above no matter how <see cref="Entries"/> arrived. The complexity trade-off
/// is explicit rather than hidden: <see cref="Add"/> is <c>O(n)</c> in the current entry count (it
/// copies <see cref="Entries"/>' backing array, the same cost <see cref="ImmutableArray{T}.Add"/>
/// always has), and <see cref="PopDue"/> is <c>O(n + k log k)</c>, where <c>n</c> is the total entry
/// count and <c>k</c> is the number of due entries -- a full scan to select the due subset, then a
/// sort of only that subset. Both are for the game- and simulation-scale entry counts (hundreds to
/// low thousands) this type targets; the public contract is storage-order independence, not this
/// specific representation, so a future release could change the internal layout without breaking
/// callers.
/// </para>
/// <para>
/// <b>Record equality is stricter than "same logical schedule," in two independent ways.</b> First,
/// it is sensitive to storage order: two schedules holding the same entries in a different
/// <see cref="Entries"/> order compare unequal even though <see cref="PopDue"/> would treat them
/// identically. Second, and more surprising, <see cref="ImmutableArray{T}"/>'s own equality compares
/// the <i>identity</i> of its backing array, not its elements -- so two schedules built by separately
/// <see cref="Add"/>-ing the exact same entries in the exact same order can also compare unequal,
/// because each <see cref="Add"/> call allocates its own backing array. Comparing two
/// <see cref="TickSchedule{TEvent}"/> values with <c>==</c> or <see cref="object.Equals(object?)"/> is
/// therefore rarely what a caller wants; compare the results of <see cref="PopDue"/> (or of
/// <see cref="Entries"/> converted to a plain array/list) element-by-element instead.
/// </para>
/// <para>
/// <b><c>default(TickSchedule{TEvent})</c> is a legal, empty schedule</b> -- the same status as
/// <c>default(Cooldown)</c>, and the same "default <see cref="ImmutableArray{T}"/> means empty"
/// reading this package's sibling types use. <see cref="Empty"/> is exactly this default value.
/// Every member treats a default <see cref="Entries"/> as <see cref="ImmutableArray{T}.Empty"/>, so
/// there is no invalid state here for an <c>EnsureValid</c> guard to reject: <see cref="Add"/> simply
/// adds the first entry, <see cref="PopDue"/> returns an empty array, <see cref="Count"/> is
/// <c>0</c>. The one place corruption can still surface is a hand-edited or truncated
/// <see cref="Entries"/> payload with duplicate <see cref="TickScheduleEntry{TEvent}.Sequence"/>
/// values (normally impossible through <see cref="Add"/>, but reachable through the
/// <see langword="init"/> accessor or a corrupted deserialization); <see cref="PopDue"/> stays total
/// even then by breaking any remaining tie with each entry's storage position as a third,
/// implementation-only sort key -- the *observable* contract is still exactly
/// <c>(DueTick, Sequence)</c> for any payload <see cref="Add"/> could have produced.
/// </para>
/// <para>
/// <b>Ticks are opaque to this type.</b> Any <see langword="long"/> is a legal
/// <see cref="TickScheduleEntry{TEvent}.DueTick"/>, including negative values -- this type only ever
/// compares ticks, never assigns them meaning. Adding an entry due at or before the schedule's own
/// notion of "now" is legal and simply makes it immediately due at the next <see cref="PopDue"/>,
/// since the schedule itself has no notion of "now" to compare against at <see cref="Add"/> time.
/// There is deliberately no <see cref="TimeProvider"/> sugar on this type -- a logical tick is not a
/// wall-clock reading.
/// </para>
/// <para>
/// <b><see cref="NextSequence"/> is exhaustible only in principle.</b> It advances by exactly one per
/// <see cref="Add"/> using checked arithmetic, so the billions-of-events-per-second rate needed to
/// exhaust a <see langword="long"/> counter is not a practical concern; when it would overflow,
/// <see cref="Add"/> throws <see cref="OverflowException"/> rather than silently wrapping into a
/// duplicate <see cref="TickScheduleEntry{TEvent}.Sequence"/>. If <see cref="NextSequence"/> is
/// corrupted to a value at or below an existing entry's <see cref="TickScheduleEntry{TEvent}.Sequence"/>
/// (via the <see langword="init"/> accessor or deserialization), subsequent <see cref="Add"/> calls
/// can produce duplicate <see cref="TickScheduleEntry{TEvent}.Sequence"/> values -- <see cref="PopDue"/>
/// remains fully deterministic regardless, via the storage-position tie-break described above.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var schedule = TickSchedule&lt;string&gt;.Empty
///     .Add("boss-respawn", dueTick: 1800)
///     .Add("wave-2", dueTick: 1800);
///
/// // ... simulation advances to tick 1800 ...
/// var due = schedule.PopDue(currentTick: 1800, out schedule);
/// // due contains both entries, "boss-respawn" before "wave-2" (insertion order, same tick).
/// </code>
/// </example>
public readonly record struct TickSchedule<TEvent>
{
    /// <summary>
    /// Gets the empty schedule. Identical in behavior to <see langword="default"/>(<see cref="TickSchedule{TEvent}"/>);
    /// see the type-level remarks for why <see langword="default"/> is a legal value here.
    /// </summary>
    public static TickSchedule<TEvent> Empty => default;

    /// <summary>
    /// Backing storage for <see cref="Entries"/>, kept private and possibly
    /// <see cref="ImmutableArray{T}.IsDefault"/> so that <see langword="default"/>(<see cref="TickSchedule{TEvent}"/>)
    /// needs no separate initialization step; <see cref="Entries"/>'s getter is the single place that
    /// normalizes it. Comparing two <see cref="TickSchedule{TEvent}"/> values compares this raw field
    /// (record structs generate equality over instance fields, not property getters), which is why
    /// record equality can observe the identity-based quirk described in the type-level remarks even
    /// though every property getter normalizes.
    /// </summary>
    private readonly ImmutableArray<TickScheduleEntry<TEvent>> _entries;

    /// <summary>
    /// Gets the schedule's entries, in storage (insertion-then-removal) order. This order carries no
    /// meaning on its own -- see the type-level remarks on why <see cref="PopDue"/>'s output does not
    /// depend on it -- but it is the serialization surface: persist and restore this property (along
    /// with <see cref="NextSequence"/>) to save and reload a schedule exactly.
    /// </summary>
    /// <remarks>
    /// This getter always returns a non-<see langword="default"/> <see cref="ImmutableArray{T}"/> --
    /// <see cref="ImmutableArray{T}.Empty"/> when the schedule is <see langword="default"/> or
    /// <see cref="Empty"/> -- specifically so that generic, reflection-driven serializers (System.Text.Json
    /// among them) that read this property directly never hit
    /// <see cref="ImmutableArray{T}"/>'s "operation cannot be performed on a default instance"
    /// exception when handed a freshly-<see langword="default"/> schedule.
    /// </remarks>
    public ImmutableArray<TickScheduleEntry<TEvent>> Entries
    {
        get => _entries.IsDefault ? ImmutableArray<TickScheduleEntry<TEvent>>.Empty : _entries;
        init => _entries = value;
    }

    /// <summary>
    /// Gets the <see cref="TickScheduleEntry{TEvent}.Sequence"/> that will be assigned to the next
    /// entry <see cref="Add"/> creates. Starts at <c>0</c> on an empty schedule and advances by one per
    /// <see cref="Add"/> call, using checked arithmetic (see the type-level remarks).
    /// </summary>
    public long NextSequence { get; init; }

    /// <summary>
    /// Gets the number of entries currently in the schedule.
    /// </summary>
    public int Count => Entries.Length;

    /// <summary>
    /// Gets a value indicating whether the schedule holds no entries.
    /// </summary>
    public bool IsEmpty => Entries.IsEmpty;

    /// <summary>
    /// Gets the smallest <see cref="TickScheduleEntry{TEvent}.DueTick"/> among the schedule's entries,
    /// or <see langword="null"/> when the schedule is empty. Useful for deciding how far a simulation
    /// loop can safely fast-forward before the next <see cref="PopDue"/> call would have anything to
    /// return.
    /// </summary>
    public long? NextDueTick
    {
        get
        {
            var entries = Entries;

            if (entries.IsEmpty)
            {
                return null;
            }

            var min = entries[0].DueTick;

            for (var i = 1; i < entries.Length; i++)
            {
                if (entries[i].DueTick < min)
                {
                    min = entries[i].DueTick;
                }
            }

            return min;
        }
    }

    /// <summary>
    /// Returns a schedule with a new entry appended, due at <paramref name="dueTick"/>.
    /// </summary>
    /// <param name="event">The event value to store. It is returned, unexecuted, by a future
    /// <see cref="PopDue"/> call -- see the type-level remarks on why this type stores values rather
    /// than delegates.</param>
    /// <param name="dueTick">The logical tick <paramref name="event"/> becomes due at. Any
    /// <see langword="long"/> is legal, including a value at or before the schedule's own notion of
    /// "now" -- such an entry is simply immediately due at the next <see cref="PopDue"/> call.</param>
    /// <returns>A schedule with the new entry appended to <see cref="Entries"/> and
    /// <see cref="NextSequence"/> advanced by one.</returns>
    /// <exception cref="OverflowException"><see cref="NextSequence"/> is <see cref="long.MaxValue"/>
    /// and cannot be advanced further; see the type-level remarks.</exception>
    public TickSchedule<TEvent> Add(TEvent @event, long dueTick)
    {
        var entry = new TickScheduleEntry<TEvent>(dueTick, NextSequence, @event);
        var nextSequence = checked(NextSequence + 1);

        return this with { Entries = Entries.Add(entry), NextSequence = nextSequence };
    }

    /// <summary>
    /// Removes and returns every entry whose <see cref="TickScheduleEntry{TEvent}.DueTick"/> is at or
    /// before <paramref name="currentTick"/> (an inclusive boundary), in dispatch order.
    /// </summary>
    /// <param name="currentTick">The current logical tick. Every due entry with
    /// <c>DueTick &lt;= currentTick</c> is popped; an entry due at <paramref name="currentTick"/> plus
    /// one, or later, is left in <paramref name="updated"/> for a future call.</param>
    /// <param name="updated">When this method returns, the schedule with the due entries removed and
    /// every remaining entry preserved in its original storage order. When nothing was due, this is
    /// exactly <see langword="this"/> (the same value, not merely an equal one) -- popping nothing is a
    /// no-op, not a rebuild, and (see the implementation note on the most common polling path below)
    /// does not allocate.</param>
    /// <returns>The due entries, ordered by <see cref="TickScheduleEntry{TEvent}.DueTick"/> ascending
    /// and then by <see cref="TickScheduleEntry{TEvent}.Sequence"/> ascending -- the permanent
    /// determinism contract described in the type-level remarks. Empty when nothing was due; this
    /// method never fails or throws for "nothing to pop", so the empty-array result is itself a legal,
    /// expected outcome rather than an error case a caller needs to guard against.</returns>
    public ImmutableArray<TickScheduleEntry<TEvent>> PopDue(long currentTick, out TickSchedule<TEvent> updated)
    {
        var entries = Entries;

        // The most common call shape in a per-tick polling loop is "nothing due yet", so check for
        // that with a plain scan before allocating anything: no List<T>, no ImmutableArray.Builder,
        // not even for an empty schedule (entries.Length == 0 simply never enters the loop below).
        var hasDue = false;

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].DueTick <= currentTick)
            {
                hasDue = true;
                break;
            }
        }

        if (!hasDue)
        {
            updated = this;
            return ImmutableArray<TickScheduleEntry<TEvent>>.Empty;
        }

        var due = new List<(TickScheduleEntry<TEvent> Entry, int StorageIndex)>();
        var remaining = ImmutableArray.CreateBuilder<TickScheduleEntry<TEvent>>();

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];

            if (entry.DueTick <= currentTick)
            {
                due.Add((entry, i));
            }
            else
            {
                remaining.Add(entry);
            }
        }

        // Every candidate carries a unique StorageIndex, so this comparer defines a total order over
        // `due` regardless of DueTick/Sequence collisions -- List<T>.Sort's lack of a stability
        // guarantee cannot introduce nondeterminism here, because no two elements ever compare equal.
        due.Sort(static (left, right) =>
        {
            var byDueTick = left.Entry.DueTick.CompareTo(right.Entry.DueTick);
            if (byDueTick != 0)
            {
                return byDueTick;
            }

            var bySequence = left.Entry.Sequence.CompareTo(right.Entry.Sequence);
            return bySequence != 0 ? bySequence : left.StorageIndex.CompareTo(right.StorageIndex);
        });

        var result = ImmutableArray.CreateBuilder<TickScheduleEntry<TEvent>>(due.Count);
        foreach (var (entry, _) in due)
        {
            result.Add(entry);
        }

        updated = this with { Entries = remaining.ToImmutable() };
        return result.ToImmutable();
    }

    /// <summary>
    /// Returns a schedule with every entry matching <paramref name="event"/> removed -- cancelling a
    /// previously scheduled event by value.
    /// </summary>
    /// <param name="event">The event value to remove. Compared using
    /// <see cref="EqualityComparer{T}.Default"/> for <typeparamref name="TEvent"/>; every entry whose
    /// <see cref="TickScheduleEntry{TEvent}.Event"/> equals this value is removed, regardless of its
    /// <see cref="TickScheduleEntry{TEvent}.DueTick"/> or <see cref="TickScheduleEntry{TEvent}.Sequence"/>.</param>
    /// <returns>A schedule with every matching entry removed and every other entry preserved in its
    /// original storage order. <see cref="NextSequence"/> is unchanged -- removed entries' sequence
    /// numbers are never reused by a later <see cref="Add"/>. When nothing matched, this is exactly
    /// <see langword="this"/> (the same value, not merely an equal one).</returns>
    public TickSchedule<TEvent> RemoveAll(TEvent @event)
    {
        var entries = Entries;

        if (entries.IsEmpty)
        {
            return this;
        }

        var comparer = EqualityComparer<TEvent>.Default;
        var remaining = ImmutableArray.CreateBuilder<TickScheduleEntry<TEvent>>(entries.Length);

        foreach (var entry in entries)
        {
            if (!comparer.Equals(entry.Event, @event))
            {
                remaining.Add(entry);
            }
        }

        return remaining.Count == entries.Length ? this : this with { Entries = remaining.ToImmutable() };
    }
}
