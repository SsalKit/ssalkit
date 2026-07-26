using System.Diagnostics;
using System.Globalization;

namespace SsalKit.RecurrenceSchedule;

/// <summary>
/// A calendar-aligned recurring boundary — "every day at 04:30 Seoul time", "every Monday at 09:00
/// UTC", "the 1st of every month at midnight New York time" — and the pure functions that answer
/// the questions a reset boundary exists to answer: when was the last one, when is the next one,
/// which window are we in, and how many have gone by since a remembered instant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Boundary semantics.</b> A boundary instant <c>b</c> belongs to the window it opens, not to
/// the one it closes: <c>CurrentWindow(b).Start == b</c>, and
/// <see cref="PreviousBoundary(DateTimeOffset)"/> returns the greatest boundary <c>&lt;= asOf</c>
/// while <see cref="NextBoundary(DateTimeOffset)"/> returns the least boundary <c>&gt; asOf</c>.
/// <see cref="HasCrossed(DateTimeOffset, DateTimeOffset)"/> follows from that: it asks whether some
/// boundary <c>b</c> satisfies <c>lastSeen &lt; b &lt;= now</c>. If <c>lastSeen</c> is itself a
/// boundary, that window has already been seen and nothing has been crossed; if <c>now</c> is
/// exactly a boundary, it has just been crossed. Because the whole comparison is on instants, the
/// classic "compare only the hour field" bug — treating 04:15 as past an 04:30 reset because
/// <c>4 &gt;= 4</c> — cannot be expressed through this API.
/// </para>
/// <para>
/// <b>Everything is a pure function of (schedule, instant).</b> Nothing here reads the ambient
/// clock; the instant is always a parameter, which is what makes schedule logic testable without
/// freezing time globally. For code that does want the current time, the
/// <see cref="RecurrenceScheduleTimeProviderExtensions"/> overloads take a
/// <see cref="TimeProvider"/> and forward its <see cref="TimeProvider.GetUtcNow"/>.
/// </para>
/// <para>
/// <b>Returned instants carry the schedule's time zone offset.</b> A boundary of a
/// <c>Asia/Seoul</c> schedule comes back as <c>+09:00</c>, one of a <c>America/New_York</c> schedule
/// as <c>-05:00</c> or <c>-04:00</c> depending on the date, and a UTC schedule as <c>+00:00</c>.
/// Comparisons are unaffected — <see cref="DateTimeOffset"/> compares instants — but the offset is
/// preserved so that formatting a boundary shows the local wall-clock time the schedule was
/// defined in.
/// </para>
/// <para>
/// <b>Range.</b> Boundaries are computed within the range of <see cref="DateTime"/>. Asking for a
/// boundary before <see cref="DateTime.MinValue"/> or after <see cref="DateTime.MaxValue"/> — which
/// requires an <c>asOf</c> at the very edge of <see cref="DateTimeOffset"/>'s range — throws
/// <see cref="ArgumentOutOfRangeException"/> from the underlying date arithmetic, because no such
/// boundary is representable.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var reset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));
///
/// if (reset.HasCrossed(player.LastQuotaReset, now))
/// {
///     player.Quota = DailyQuota;
///     player.LastQuotaReset = reset.PreviousBoundary(now);
/// }
///
/// var missedRewards = reset.CountBoundaries(player.LastLogin, now);
/// </code>
/// </example>
public sealed class RecurrenceSchedule
{
    private const int DaysPerWeek = 7;

    private const int MonthsPerYear = 12;

    private readonly RecurrenceKind _kind;
    private readonly TimeOnly _timeOfDay;
    private readonly DayOfWeek _dayOfWeek;
    private readonly int _dayOfMonth;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// The distance between two consecutive occurrence keys: 1 day number for a daily schedule,
    /// 7 for a weekly one, 1 month index for a monthly one. See <see cref="OccurrenceOnOrBefore"/>
    /// for what a key is.
    /// </summary>
    private readonly int _occurrenceStep;

    private RecurrenceSchedule(
        RecurrenceKind kind,
        TimeOnly timeOfDay,
        DayOfWeek dayOfWeek,
        int dayOfMonth,
        TimeZoneInfo timeZone)
    {
        _kind = kind;
        _timeOfDay = timeOfDay;
        _dayOfWeek = dayOfWeek;
        _dayOfMonth = dayOfMonth;
        _timeZone = timeZone;
        _occurrenceStep = kind == RecurrenceKind.Weekly ? DaysPerWeek : 1;
    }

    /// <summary>
    /// Creates a schedule that recurs once every calendar day at the given wall-clock time.
    /// </summary>
    /// <param name="atTimeOfDay">The wall-clock time of day the boundary falls on, interpreted in
    /// <paramref name="timeZone"/>.</param>
    /// <param name="timeZone">The time zone whose wall clock the schedule is defined against.
    /// <see langword="null"/> (the default) means <see cref="TimeZoneInfo.Utc"/>.</param>
    /// <returns>The schedule.</returns>
    /// <remarks>
    /// Every calendar day has exactly one boundary, including days on which a daylight-saving
    /// transition removes or repeats the scheduled wall-clock time. See
    /// <see cref="PreviousBoundary(DateTimeOffset)"/> for the transition rules.
    /// </remarks>
    public static RecurrenceSchedule Daily(TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null) =>
        new(RecurrenceKind.Daily, atTimeOfDay, default, 0, timeZone ?? TimeZoneInfo.Utc);

    /// <summary>
    /// Creates a schedule that recurs once every calendar week, on the given day of the week at the
    /// given wall-clock time.
    /// </summary>
    /// <param name="dayOfWeek">The day of the week the boundary falls on, as reckoned in
    /// <paramref name="timeZone"/>.</param>
    /// <param name="atTimeOfDay">The wall-clock time of day the boundary falls on, interpreted in
    /// <paramref name="timeZone"/>.</param>
    /// <param name="timeZone">The time zone whose wall clock the schedule is defined against.
    /// <see langword="null"/> (the default) means <see cref="TimeZoneInfo.Utc"/>.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dayOfWeek"/> is not one of the
    /// seven defined <see cref="System.DayOfWeek"/> values.</exception>
    public static RecurrenceSchedule Weekly(
        DayOfWeek dayOfWeek,
        TimeOnly atTimeOfDay,
        TimeZoneInfo? timeZone = null)
    {
        if (dayOfWeek is < DayOfWeek.Sunday or > DayOfWeek.Saturday)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayOfWeek),
                dayOfWeek,
                "The day of the week must be one of the seven defined DayOfWeek values.");
        }

        return new RecurrenceSchedule(RecurrenceKind.Weekly, atTimeOfDay, dayOfWeek, 0, timeZone ?? TimeZoneInfo.Utc);
    }

    /// <summary>
    /// Creates a schedule that recurs once every calendar month, on the given day of the month at
    /// the given wall-clock time.
    /// </summary>
    /// <param name="dayOfMonth">The day of the month the boundary falls on, from 1 to 31. Months
    /// shorter than <paramref name="dayOfMonth"/> clamp to their last day, so a schedule on day 31
    /// falls on 28 February in a common year, 29 February in a leap year, and 30 April.</param>
    /// <param name="atTimeOfDay">The wall-clock time of day the boundary falls on, interpreted in
    /// <paramref name="timeZone"/>.</param>
    /// <param name="timeZone">The time zone whose wall clock the schedule is defined against.
    /// <see langword="null"/> (the default) means <see cref="TimeZoneInfo.Utc"/>.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dayOfMonth"/> is less than 1
    /// or greater than 31.</exception>
    /// <remarks>
    /// Clamping keeps the "exactly one boundary per month" invariant that
    /// <see cref="CountBoundaries(DateTimeOffset, DateTimeOffset)"/> relies on: a monthly schedule
    /// never skips a month, whatever day it is anchored to.
    /// </remarks>
    public static RecurrenceSchedule Monthly(
        int dayOfMonth,
        TimeOnly atTimeOfDay,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dayOfMonth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dayOfMonth, 31);

        return new RecurrenceSchedule(
            RecurrenceKind.Monthly,
            atTimeOfDay,
            default,
            dayOfMonth,
            timeZone ?? TimeZoneInfo.Utc);
    }

    /// <summary>
    /// Returns the most recent boundary at or before <paramref name="asOf"/> — the greatest
    /// boundary <c>b</c> with <c>b &lt;= asOf</c>. When <paramref name="asOf"/> is itself a
    /// boundary, it is returned unchanged.
    /// </summary>
    /// <param name="asOf">The instant to look back from.</param>
    /// <returns>The boundary, carrying the schedule time zone's UTC offset for that date.</returns>
    /// <remarks>
    /// <para>
    /// <b>Daylight-saving contract (fixed for the lifetime of this type).</b> The scheduled time is
    /// a <i>wall-clock</i> time in the schedule's time zone, and the two ways a wall clock can
    /// misbehave are resolved as follows.
    /// </para>
    /// <para>
    /// 1. <b>A scheduled time that does not exist</b> — the clock jumps forward over it, as
    /// 02:00→03:00 does to an 02:30 schedule — moves to <b>the first valid instant after the
    /// gap</b>: 03:00, the transition itself, not 03:30. The boundary is never dropped, so a daily
    /// schedule still has exactly one boundary on that day and
    /// <see cref="CountBoundaries(DateTimeOffset, DateTimeOffset)"/> stays equal to the number of
    /// elapsed days.
    /// </para>
    /// <para>
    /// 2. <b>A scheduled time that happens twice</b> — the clock falls back over it, as
    /// 02:00→01:00 does to an 01:30 schedule — resolves to the <b>first</b> occurrence, the one
    /// under the pre-transition (larger) UTC offset. The schedule does not fire twice that day.
    /// </para>
    /// <para>
    /// 3. Any other wall-clock time uses the zone's offset for that date, so the boundary sits at
    /// the intended local time year-round rather than drifting with the seasons.
    /// </para>
    /// <para>
    /// These rules are a <b>versioned contract</b>. Boundaries get persisted — "the reset this
    /// player last saw" — and comparing a stored instant against a recomputed one only works if the
    /// computation never changes. Like the algorithm contract of a seeded PRNG, this behaviour will
    /// not be altered in a patch or minor release; a different resolution policy would require a
    /// new type.
    /// </para>
    /// <para>
    /// Time zone identifiers follow <see cref="TimeZoneInfo"/>'s own resolution. On .NET 6 and
    /// later, <see cref="TimeZoneInfo.FindSystemTimeZoneById(string)"/> accepts IANA identifiers
    /// such as <c>America/New_York</c> on Windows as well as Unix, provided ICU is available.
    /// </para>
    /// </remarks>
    public DateTimeOffset PreviousBoundary(DateTimeOffset asOf) => PreviousCore(asOf).Boundary;

    /// <summary>
    /// Returns the next boundary strictly after <paramref name="asOf"/> — the least boundary
    /// <c>b</c> with <c>b &gt; asOf</c>. When <paramref name="asOf"/> is itself a boundary, the
    /// following one is returned.
    /// </summary>
    /// <param name="asOf">The instant to look forward from.</param>
    /// <returns>The boundary, carrying the schedule time zone's UTC offset for that date. The
    /// daylight-saving rules documented on <see cref="PreviousBoundary(DateTimeOffset)"/> apply
    /// identically.</returns>
    public DateTimeOffset NextBoundary(DateTimeOffset asOf) =>
        ResolveBoundary(OccurrenceDate(PreviousCore(asOf).Key + _occurrenceStep));

    /// <summary>
    /// Returns how much time is left before the next boundary — exactly
    /// <c>NextBoundary(asOf) - asOf</c>.
    /// </summary>
    /// <param name="asOf">The instant to measure from.</param>
    /// <returns>The time remaining in the window <paramref name="asOf"/> belongs to. <b>Always
    /// strictly positive</b>, never zero and never negative: <see cref="NextBoundary"/> is strict
    /// (<c>b &gt; asOf</c>), so an <paramref name="asOf"/> that is itself a boundary reports the
    /// full length of the window it just opened rather than <see cref="TimeSpan.Zero"/>.</returns>
    /// <remarks>
    /// The duration is measured between absolute instants, so a window shortened or lengthened by a
    /// daylight-saving transition reports its real elapsed length (23 or 25 hours for a daily
    /// schedule in a one-hour zone), not a nominal 24.
    /// </remarks>
    public TimeSpan UntilNext(DateTimeOffset asOf) => NextBoundary(asOf) - asOf;

    /// <summary>
    /// Returns the half-open window <c>[PreviousBoundary(asOf), NextBoundary(asOf))</c> that
    /// <paramref name="asOf"/> belongs to — the current "reset period".
    /// </summary>
    /// <param name="asOf">The instant to locate.</param>
    /// <returns>The window containing <paramref name="asOf"/>. Consecutive windows tile the
    /// timeline exactly, so an instant belongs to precisely one of them, and a boundary instant
    /// opens its window rather than closing the previous one.</returns>
    public TimeWindow CurrentWindow(DateTimeOffset asOf)
    {
        var (key, start) = PreviousCore(asOf);
        return new TimeWindow(start, ResolveBoundary(OccurrenceDate(key + _occurrenceStep)));
    }

    /// <summary>
    /// Returns the window <paramref name="offset"/> steps away from the one containing
    /// <paramref name="asOf"/> — the previous reset period, the one before that, or a future one.
    /// </summary>
    /// <param name="asOf">The instant whose window the offset is counted from.</param>
    /// <param name="offset">How many windows to move: <c>0</c> is the window containing
    /// <paramref name="asOf"/> and is identical to <see cref="CurrentWindow"/>, negative values
    /// move into the past, positive values into the future.</param>
    /// <returns>The half-open window, computed in constant time from occurrence arithmetic rather
    /// than by stepping — a thousand windows back costs what one costs.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> moves the window
    /// outside the range of <see cref="DateTime"/>. The arithmetic is done in 64 bits and the
    /// result range-checked, so an extreme offset such as <see cref="int.MinValue"/> throws rather
    /// than silently wrapping around to a nearby window.</exception>
    /// <remarks>
    /// <para>
    /// Consecutive offsets tile the timeline exactly, as consecutive windows do:
    /// <c>WindowAt(asOf, n).End == WindowAt(asOf, n + 1).Start</c> for every <c>n</c>.
    /// </para>
    /// <para>
    /// The obvious use is a "compared to the previous period" figure — yesterday's numbers against
    /// today's, last week's against this week's:
    /// </para>
    /// <code>
    /// var today = schedule.CurrentWindow(now);
    /// var yesterday = schedule.WindowAt(now, -1);
    /// </code>
    /// <para>
    /// Windows either side of a daylight-saving transition keep their real elapsed length, so
    /// <c>yesterday.Duration</c> may legitimately differ from <c>today.Duration</c>.
    /// </para>
    /// </remarks>
    public TimeWindow WindowAt(DateTimeOffset asOf, int offset)
    {
        var shifted = PreviousCore(asOf).Key + ((long)offset * _occurrenceStep);

        if (shifted is < int.MinValue or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "The offset moves the window outside the range of representable dates.");
        }

        var key = (int)shifted;
        return new TimeWindow(
            ResolveBoundary(OccurrenceDate(key)),
            ResolveBoundary(OccurrenceDate(key + _occurrenceStep)));
    }

    /// <summary>
    /// Determines whether at least one boundary lies in the half-open interval
    /// <c>(lastSeen, now]</c> — that is, whether the schedule has fired since
    /// <paramref name="lastSeen"/>.
    /// </summary>
    /// <param name="lastSeen">The previously observed instant, typically a persisted "last reset"
    /// value.</param>
    /// <param name="now">The current instant.</param>
    /// <returns><see langword="true"/> if a boundary <c>b</c> exists with
    /// <c>lastSeen &lt; b &lt;= now</c>; otherwise, <see langword="false"/>. A
    /// <paramref name="lastSeen"/> that is exactly a boundary counts as having seen that window
    /// already, a <paramref name="now"/> that is exactly a boundary counts as having just crossed
    /// it, and a <paramref name="now"/> earlier than <paramref name="lastSeen"/> yields
    /// <see langword="false"/>.</returns>
    /// <remarks>
    /// Equivalent to <c>CountBoundaries(lastSeen, now) &gt; 0</c>, but cheaper: it only has to find
    /// the first boundary after <paramref name="lastSeen"/>.
    /// </remarks>
    public bool HasCrossed(DateTimeOffset lastSeen, DateTimeOffset now) => NextBoundary(lastSeen) <= now;

    /// <summary>
    /// Counts the boundaries in the half-open interval <c>(lastSeen, now]</c> — how many times the
    /// schedule has fired since <paramref name="lastSeen"/>. Useful for granting the rewards, quota
    /// refills or billing periods that accrued while a caller was away.
    /// </summary>
    /// <param name="lastSeen">The previously observed instant, exclusive.</param>
    /// <param name="now">The current instant, inclusive.</param>
    /// <returns>The number of boundaries crossed, or <c>0</c> when <paramref name="now"/> is at or
    /// before <paramref name="lastSeen"/>.</returns>
    /// <remarks>
    /// Computed in constant time from calendar arithmetic rather than by stepping through
    /// boundaries, so a ten-year gap costs the same as a one-day gap.
    /// </remarks>
    public int CountBoundaries(DateTimeOffset lastSeen, DateTimeOffset now)
    {
        if (now <= lastSeen)
        {
            return 0;
        }

        return (PreviousCore(now).Key - PreviousCore(lastSeen).Key) / _occurrenceStep;
    }

    /// <summary>
    /// Enumerates, in ascending order, every boundary in the half-open interval
    /// <c>(<paramref name="from"/>, <paramref name="to"/>]</c> — the same interval
    /// <see cref="CountBoundaries(DateTimeOffset, DateTimeOffset)"/> counts, so the sequence always
    /// has exactly <c>CountBoundaries(from, to)</c> elements.
    /// </summary>
    /// <param name="from">The lower bound, <b>exclusive</b>: a boundary exactly at
    /// <paramref name="from"/> is not yielded.</param>
    /// <param name="to">The upper bound, <b>inclusive</b>: a boundary exactly at
    /// <paramref name="to"/> is yielded.</param>
    /// <returns>The boundaries, each carrying the schedule time zone's UTC offset for its date. An
    /// empty sequence when <paramref name="to"/> is at or before <paramref name="from"/>. The
    /// sequence can be enumerated more than once; each pass recomputes the boundaries.</returns>
    /// <remarks>
    /// <para>
    /// <b>Lazily evaluated, and O(number of boundaries).</b> Nothing is computed until the sequence
    /// is enumerated, and each boundary costs one time-zone resolution — unlike
    /// <see cref="CountBoundaries(DateTimeOffset, DateTimeOffset)"/>, which answers "how many" in
    /// constant time. Enumerating a decade of a daily schedule really does resolve 3,653 instants,
    /// so reach for the count when the count is all that is wanted, and bound the interval (or
    /// <c>Take</c>) before enumerating a wide one.
    /// </para>
    /// <para>
    /// <b>There are no arguments to validate</b> — every pair of instants is meaningful, reversed
    /// ones included — so the usual deferred-execution caveat about arguments being checked late
    /// does not apply here. An interval at the very edge of the representable range still throws
    /// <see cref="ArgumentOutOfRangeException"/> from the underlying date arithmetic, and, because
    /// execution is deferred, it does so from the first
    /// <see cref="System.Collections.IEnumerator.MoveNext"/> rather than from the call to this
    /// method.
    /// </para>
    /// </remarks>
    public IEnumerable<DateTimeOffset> EnumerateBoundaries(DateTimeOffset from, DateTimeOffset to)
    {
        var key = PreviousCore(from).Key;

        while (true)
        {
            key += _occurrenceStep;
            var boundary = ResolveBoundary(OccurrenceDate(key));

            if (boundary > to)
            {
                yield break;
            }

            yield return boundary;
        }
    }

    /// <summary>
    /// Returns a human-readable description of the cadence, such as <c>Daily 04:30 @ UTC</c>,
    /// <c>Weekly Monday 09:00 @ Asia/Seoul</c>, or <c>Monthly day 31 00:00 @ America/New_York</c>.
    /// </summary>
    /// <returns>The description, always rendered with the invariant culture and the time zone's
    /// <see cref="TimeZoneInfo.Id"/> so that it does not vary with the ambient culture. The time of
    /// day is shown as <c>HH:mm</c>, extended to <c>HH:mm:ss</c> or <c>HH:mm:ss.fffffff</c> only
    /// when the schedule carries seconds or a fraction of one.</returns>
    /// <remarks>
    /// <b>For diagnostics — logs, debugger windows, error messages — and not a parsing contract.</b>
    /// Unlike the daylight-saving rules documented on
    /// <see cref="PreviousBoundary(DateTimeOffset)"/>, this format is free to be improved in any
    /// release. Do not persist it, parse it, or assert on it outside of tests you own.
    /// </remarks>
    public override string ToString()
    {
        var timeOfDay = FormatTimeOfDay();

        return _kind switch
        {
            RecurrenceKind.Daily => $"Daily {timeOfDay} @ {_timeZone.Id}",
            RecurrenceKind.Weekly => $"Weekly {_dayOfWeek} {timeOfDay} @ {_timeZone.Id}",
            _ => $"Monthly day {_dayOfMonth} {timeOfDay} @ {_timeZone.Id}",
        };
    }

    /// <summary>
    /// Renders the scheduled time of day at the shortest resolution that loses nothing: whole
    /// minutes as <c>HH:mm</c>, whole seconds as <c>HH:mm:ss</c>, and anything finer with its full
    /// tick precision.
    /// </summary>
    private string FormatTimeOfDay()
    {
        if (_timeOfDay.Ticks % TimeSpan.TicksPerMinute == 0)
        {
            return _timeOfDay.ToString(@"HH\:mm", CultureInfo.InvariantCulture);
        }

        return _timeOfDay.Ticks % TimeSpan.TicksPerSecond == 0
            ? _timeOfDay.ToString(@"HH\:mm\:ss", CultureInfo.InvariantCulture)
            : _timeOfDay.ToString(@"HH\:mm\:ss\.fffffff", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Locates the occurrence that opens the window containing <paramref name="asOf"/>, returning
    /// both its key and its resolved boundary instant.
    /// </summary>
    private (int Key, DateTimeOffset Boundary) PreviousCore(DateTimeOffset asOf)
    {
        var wallClock = TimeZoneInfo.ConvertTime(asOf, _timeZone).DateTime;
        var key = OccurrenceOnOrBefore(DateOnly.FromDateTime(wallClock));
        var boundary = ResolveBoundary(OccurrenceDate(key));

        // The occurrence whose calendar date is on or before the date of asOf can still resolve to
        // an instant after asOf (asOf falls earlier in the day than the scheduled time). One step
        // back is always enough: occurrences are at least a day apart, which no daylight-saving
        // shift can close.
        while (boundary > asOf)
        {
            key -= _occurrenceStep;
            boundary = ResolveBoundary(OccurrenceDate(key));
        }

        Debug.Assert(
            ResolveBoundary(OccurrenceDate(key + _occurrenceStep)) > asOf,
            "The occurrence following the located one must resolve to an instant after asOf.");

        return (key, boundary);
    }

    /// <summary>
    /// Turns the calendar date of an occurrence into the instant the boundary actually falls on,
    /// applying the daylight-saving contract documented on
    /// <see cref="PreviousBoundary(DateTimeOffset)"/>.
    /// </summary>
    private DateTimeOffset ResolveBoundary(DateOnly date)
    {
        var wallClock = date.ToDateTime(_timeOfDay);

        if (_timeZone.IsInvalidTime(wallClock))
        {
            // Rule 1: the scheduled wall-clock time was skipped by a forward transition. The
            // boundary moves to the transition instant itself, the first valid time after the gap.
            return FirstInstantAfterGap(wallClock);
        }

        if (_timeZone.IsAmbiguousTime(wallClock))
        {
            // Rule 2: the scheduled wall-clock time happens twice. Take the first occurrence, which
            // is the one under the larger (pre-transition) offset — a larger offset means an
            // earlier absolute instant for the same wall clock. TimeZoneInfo.GetUtcOffset would
            // instead report the zone's standard-time offset here, which is the *second*
            // occurrence whenever standard time follows daylight saving time, so the ambiguous
            // offsets are resolved explicitly.
            return new DateTimeOffset(wallClock, _timeZone.GetAmbiguousTimeOffsets(wallClock).Max());
        }

        // Rule 3: an ordinary wall-clock time, at the zone's offset for that date.
        return new DateTimeOffset(wallClock, _timeZone.GetUtcOffset(wallClock));
    }

    /// <summary>
    /// Returns the first instant whose wall-clock time in this schedule's zone is at or after
    /// <paramref name="skippedWallClock"/>, given that the wall-clock time itself was skipped by a
    /// forward transition. That instant is the transition, expressed with the post-transition
    /// offset.
    /// </summary>
    private DateTimeOffset FirstInstantAfterGap(DateTime skippedWallClock)
    {
        // Offsets are bounded by ±14 hours, so an instant a day either side of the skipped
        // wall-clock time (read here as a UTC instant) is unambiguously on one side of the
        // transition or the other.
        var probe = DateTime.SpecifyKind(skippedWallClock, DateTimeKind.Utc);
        var offsetBefore = _timeZone.GetUtcOffset(probe.AddDays(-1));
        var offsetAfter = _timeZone.GetUtcOffset(probe.AddDays(1));

        // The wall clock is invalid precisely because it falls inside [gapStart, gapStart + delta),
        // where the transition instant is gapStart - offsetBefore. Reading the skipped wall clock
        // with the post-transition offset therefore lands strictly before the transition, and with
        // the pre-transition offset at or after it — bracketing the transition in `delta` of time.
        var beforeTransition = DateTime.SpecifyKind(skippedWallClock - offsetAfter, DateTimeKind.Utc);
        var atOrAfterTransition = DateTime.SpecifyKind(skippedWallClock - offsetBefore, DateTimeKind.Utc);

        while (atOrAfterTransition - beforeTransition > TimeSpan.FromTicks(1))
        {
            var middle = beforeTransition.AddTicks((atOrAfterTransition - beforeTransition).Ticks / 2);
            if (_timeZone.GetUtcOffset(middle) == offsetAfter)
            {
                atOrAfterTransition = middle;
            }
            else
            {
                beforeTransition = middle;
            }
        }

        return new DateTimeOffset(atOrAfterTransition, TimeSpan.Zero).ToOffset(offsetAfter);
    }

    /// <summary>
    /// Returns the key of the latest occurrence whose calendar date is on or before
    /// <paramref name="date"/>. A key is a day number for daily and weekly schedules and a month
    /// index for monthly ones; consecutive occurrences are <see cref="_occurrenceStep"/> apart, so
    /// the count of occurrences between two keys is their difference divided by that step.
    /// </summary>
    private int OccurrenceOnOrBefore(DateOnly date) => _kind switch
    {
        RecurrenceKind.Daily => date.DayNumber,
        RecurrenceKind.Weekly => date.DayNumber - (((int)date.DayOfWeek - (int)_dayOfWeek + DaysPerWeek) % DaysPerWeek),
        _ => MonthlyOccurrenceOnOrBefore(date),
    };

    private int MonthlyOccurrenceOnOrBefore(DateOnly date)
    {
        var monthIndex = MonthIndex(date.Year, date.Month);
        var scheduledDay = Math.Min(_dayOfMonth, DateTime.DaysInMonth(date.Year, date.Month));
        return scheduledDay <= date.Day ? monthIndex : monthIndex - 1;
    }

    /// <summary>
    /// Returns the calendar date an occurrence key falls on, clamping a monthly schedule's day to
    /// the length of the month.
    /// </summary>
    private DateOnly OccurrenceDate(int key)
    {
        if (_kind != RecurrenceKind.Monthly)
        {
            return DateOnly.FromDayNumber(key);
        }

        var year = (key / MonthsPerYear) + 1;
        var month = (key % MonthsPerYear) + 1;
        return new DateOnly(year, month, Math.Min(_dayOfMonth, DateTime.DaysInMonth(year, month)));
    }

    private static int MonthIndex(int year, int month) => ((year - 1) * MonthsPerYear) + (month - 1);
}
