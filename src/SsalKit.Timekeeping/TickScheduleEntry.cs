namespace SsalKit.Timekeeping;

/// <summary>
/// A single pending entry in a <see cref="TickSchedule{TEvent}"/>: an event value due at a specific
/// logical tick, tagged with the order in which it was <see cref="TickSchedule{TEvent}.Add"/>-ed.
/// </summary>
/// <typeparam name="TEvent">The event value the schedule carries and later hands back unexecuted --
/// delegates and other non-serializable callbacks are deliberately unsupported; see the type-level
/// remarks on <see cref="TickSchedule{TEvent}"/>. No constraint is imposed, but a <see langword="notnull"/>
/// type (an enum, a primitive id, or a record) is recommended so that
/// <see cref="TickSchedule{TEvent}.RemoveAll"/>'s <see cref="EqualityComparer{T}.Default"/> comparison,
/// and whatever serializer is used on a persisted schedule, behave predictably.</typeparam>
/// <param name="DueTick">The logical tick at which this entry becomes due.
/// <see cref="TickSchedule{TEvent}.PopDue"/> returns an entry once the queried tick is at or after
/// this value -- the boundary is inclusive. See the type-level remarks on
/// <see cref="TickSchedule{TEvent}"/> for the full determinism contract.</param>
/// <param name="Sequence">The insertion order <see cref="TickSchedule{TEvent}.Add"/> assigned this
/// entry, used as the tie-break between entries that share the same <see cref="DueTick"/> (lower
/// <see cref="Sequence"/> pops first -- first-in, first-out). This is deliberately not the same thing
/// as the entry's position within <see cref="TickSchedule{TEvent}.Entries"/>: entries are never moved
/// or re-sorted in storage, so <see cref="Sequence"/> is the only reliable record of insertion order
/// once a schedule has been round-tripped through a serializer.</param>
/// <param name="Event">The event value the caller supplied to <see cref="TickSchedule{TEvent}.Add"/>.
/// The schedule only ever stores and later replays this value; deciding what it means and executing it
/// is entirely the caller's responsibility.</param>
public readonly record struct TickScheduleEntry<TEvent>(long DueTick, long Sequence, TEvent Event);
