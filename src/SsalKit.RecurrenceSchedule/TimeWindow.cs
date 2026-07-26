namespace SsalKit.RecurrenceSchedule;

/// <summary>
/// A half-open interval of time <c>[Start, End)</c> — the start instant belongs to the window, the
/// end instant does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Half-open is the only containment rule.</b> Every membership question this type answers uses
/// <c>Start &lt;= instant &lt; End</c>, with no "inclusive" variant. That is what makes adjacent
/// windows tile time exactly: <c>[a, b)</c> followed by <c>[b, c)</c> neither overlap (no instant
/// is in both) nor leak (no instant between <c>a</c> and <c>c</c> is in neither). A codebase that
/// mixes inclusive and exclusive end bounds gets double counting at <c>b</c> from one pair of
/// methods and a hole at <c>b</c> from another; this type refuses to offer the choice.
/// </para>
/// <para>
/// <b>Comparison is by absolute instant, never by offset notation.</b>
/// <see cref="DateTimeOffset"/> denotes a point on the timeline, and both this type's operations
/// (<see cref="Contains"/>, <see cref="Overlaps"/>, <see cref="Intersect"/>, <see cref="Clamp"/>)
/// and its value equality compare those points. <c>2026-01-01T00:00:00+00:00</c> and
/// <c>2026-01-01T09:00:00+09:00</c> are the same instant, so windows written with either notation
/// are equal and behave identically. The offsets are still carried through into
/// <see cref="Start"/>, <see cref="End"/> and <see cref="ToString"/> for display, so a window
/// produced by a <see cref="RecurrenceSchedule"/> keeps the local offset of its time zone.
/// </para>
/// <para>
/// <b>Empty windows are legal, unordered ones are not.</b> <c>Start == End</c> produces an empty
/// window that contains nothing and overlaps nothing; <c>Start &gt; End</c> throws, because a
/// window whose end precedes its start has no meaningful duration or containment.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var window = new TimeWindow(start, start.AddDays(1));
/// if (window.Contains(now)) { /* ... */ }
/// </code>
/// </example>
public readonly record struct TimeWindow
{
    /// <summary>
    /// Initializes a new half-open window <c>[<paramref name="start"/>, <paramref name="end"/>)</c>.
    /// </summary>
    /// <param name="start">The inclusive start of the window.</param>
    /// <param name="end">The exclusive end of the window. May equal <paramref name="start"/>, which
    /// produces an empty window.</param>
    /// <exception cref="ArgumentException"><paramref name="end"/> is earlier than
    /// <paramref name="start"/> as an absolute instant.</exception>
    public TimeWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (start > end)
        {
            throw new ArgumentException(
                $"The end of a time window must not precede its start (start: {start:O}, end: {end:O}).",
                nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the inclusive start of the window. This instant is contained in the window.
    /// </summary>
    public DateTimeOffset Start { get; }

    /// <summary>
    /// Gets the exclusive end of the window. This instant is <i>not</i> contained in the window; it
    /// is the start of whatever comes next.
    /// </summary>
    public DateTimeOffset End { get; }

    /// <summary>
    /// Gets the elapsed time between <see cref="Start"/> and <see cref="End"/>. Never negative;
    /// <see cref="TimeSpan.Zero"/> for an empty window.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Determines whether <paramref name="instant"/> falls inside this window, using the half-open
    /// rule <c>Start &lt;= instant &lt; End</c>.
    /// </summary>
    /// <param name="instant">The instant to test. Compared as an absolute point in time, so its
    /// UTC offset notation does not matter.</param>
    /// <returns><see langword="true"/> if the instant is in the window; otherwise,
    /// <see langword="false"/>. Always <see langword="false"/> for an empty window.</returns>
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    /// <summary>
    /// Determines whether this window and <paramref name="other"/> share at least one instant.
    /// </summary>
    /// <param name="other">The window to test against.</param>
    /// <returns><see langword="true"/> if the intersection is non-empty; otherwise,
    /// <see langword="false"/>. Windows that merely touch (<c>[a, b)</c> and <c>[b, c)</c>) do not
    /// overlap, and an empty window never overlaps anything.</returns>
    public bool Overlaps(TimeWindow other) => Intersect(other) is not null;

    /// <summary>
    /// Computes the overlap between this window and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The window to intersect with.</param>
    /// <returns>The shared half-open interval, or <see langword="null"/> when the windows do not
    /// share any instant. Symmetric: <c>a.Intersect(b)</c> and <c>b.Intersect(a)</c> describe the
    /// same interval.</returns>
    public TimeWindow? Intersect(TimeWindow other)
    {
        var start = Start > other.Start ? Start : other.Start;
        var end = End < other.End ? End : other.End;
        return start < end ? new TimeWindow(start, end) : null;
    }

    /// <summary>
    /// Restricts <paramref name="instant"/> to this window's bounds.
    /// </summary>
    /// <param name="instant">The instant to clamp.</param>
    /// <returns><see cref="Start"/> if the instant precedes the window, <see cref="End"/> if it is
    /// at or past the window's end, otherwise the instant itself.</returns>
    /// <remarks>
    /// Clamping targets the <i>closed</i> range <c>[Start, End]</c>, so the result of clamping a
    /// late instant is <see cref="End"/> — an instant that <see cref="Contains"/> reports as
    /// outside the window. That is deliberate: clamping answers "how far into this window did we
    /// get", and the answer for something that ran past the end is the end.
    /// </remarks>
    public DateTimeOffset Clamp(DateTimeOffset instant)
    {
        if (instant < Start)
        {
            return Start;
        }

        return instant > End ? End : instant;
    }
}
