# SsalKit.RecurrenceSchedule — AI contract sheet

Time-zone-aware recurring reset boundaries (daily / weekly / monthly) and half-open time-window arithmetic, written as **pure functions of `(schedule, instant)`**, with a permanently fixed daylight-saving contract and `TimeProvider` overloads.

- **TFM:** `net10.0`. **Package dependencies:** none (BCL only). No source generator, no analyzer.
- **Namespace:** `SsalKit.RecurrenceSchedule` (note: the namespace and the main type share a name).
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 1. API surface

### Pick the right member

| Question | Use |
|---|---|
| "Has the reset happened since we last looked?" | `HasCrossed(lastSeen, now)` |
| "How many resets did they miss?" | `CountBoundaries(lastSeen, now)` — O(1) |
| "Which ones, exactly?" | `EnumerateBoundaries(from, to)` — O(number of boundaries) |
| "When was the last / next one?" | `PreviousBoundary(asOf)` / `NextBoundary(asOf)` |
| "How long is left in this period?" | `UntilNext(asOf)` — always strictly positive |
| "Which period are we in?" | `CurrentWindow(asOf)` |
| "Compared to the previous period" | `WindowAt(asOf, -1)` — O(1) at any offset |
| Caller already holds an injected clock | the `RecurrenceScheduleTimeProviderExtensions` overloads |
| Interval containment / overlap / clamping | `TimeWindow` |

### `RecurrenceSchedule` — `sealed class`

| Member | Contract |
|---|---|
| `static RecurrenceSchedule Daily(TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | One boundary per calendar day. `null` zone means `TimeZoneInfo.Utc`. |
| `static RecurrenceSchedule Weekly(DayOfWeek dayOfWeek, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | `ArgumentOutOfRangeException` for an undefined `DayOfWeek`. |
| `static RecurrenceSchedule Monthly(int dayOfMonth, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | `dayOfMonth` 1–31, else `ArgumentOutOfRangeException`. Shorter months **clamp to their last day**, so every month gets exactly one boundary. |
| `DateTimeOffset PreviousBoundary(DateTimeOffset asOf)` | Greatest boundary `b <= asOf`. Returns `asOf` unchanged when it is itself a boundary. |
| `DateTimeOffset NextBoundary(DateTimeOffset asOf)` | Least boundary `b > asOf` — **strict**, so a boundary returns the following one. |
| `TimeSpan UntilNext(DateTimeOffset asOf)` | Exactly `NextBoundary(asOf) - asOf`. **Always strictly positive**, never zero. Measured between absolute instants, so a DST-affected window reports 23 h or 25 h, not a nominal 24. |
| `TimeWindow CurrentWindow(DateTimeOffset asOf)` | `[PreviousBoundary(asOf), NextBoundary(asOf))`. |
| `TimeWindow WindowAt(DateTimeOffset asOf, int offset)` | `0` == `CurrentWindow`; negative is past, positive is future. O(1) at any offset. `ArgumentOutOfRangeException` (naming `offset`) when the 64-bit arithmetic leaves the representable range. |
| `bool HasCrossed(DateTimeOffset lastSeen, DateTimeOffset now)` | Whether some boundary `b` satisfies `lastSeen < b <= now`. `false` when `now < lastSeen`. |
| `int CountBoundaries(DateTimeOffset lastSeen, DateTimeOffset now)` | Count of such `b`. `0` when `now <= lastSeen`. O(1) closed-form calendar arithmetic. |
| `IEnumerable<DateTimeOffset> EnumerateBoundaries(DateTimeOffset from, DateTimeOffset to)` | Same half-open interval `(from, to]`, ascending, **lazy**, exactly `CountBoundaries(from, to)` elements. Re-enumerable; each pass recomputes. |
| `override string ToString()` | `Daily 04:30 @ UTC`, `Weekly Monday 09:00 @ Asia/Seoul`, `Monthly day 31 00:00 @ America/New_York`. Invariant culture, `TimeZoneInfo.Id`; `HH:mm` grows to `HH:mm:ss`/`HH:mm:ss.fffffff` only when the schedule is that precise. |

Returned boundaries carry **the schedule zone's UTC offset for that date** (`+09:00` for Seoul, `-05:00`/`-04:00` for New York). Comparisons are unaffected — `DateTimeOffset` compares instants — the offset is preserved for display.

### `TimeWindow` — `readonly record struct`

| Member | Contract |
|---|---|
| `TimeWindow(DateTimeOffset start, DateTimeOffset end)` | Half-open `[start, end)`. `start == end` is a legal empty window; `start > end` throws `ArgumentException`. |
| `DateTimeOffset Start { get; }` / `End { get; }` | Inclusive start, **exclusive** end. |
| `TimeSpan Duration { get; }` | `End - Start`; never negative. |
| `bool Contains(DateTimeOffset instant)` | `Start <= instant < End`. Always `false` for an empty window. |
| `bool Overlaps(TimeWindow other)` | Non-empty intersection. Touching windows (`[a,b)` and `[b,c)`) do **not** overlap. |
| `TimeWindow? Intersect(TimeWindow other)` | The shared interval, or `null`. Symmetric. |
| `DateTimeOffset Clamp(DateTimeOffset instant)` | Clamps to the **closed** range `[Start, End]`, so an overrun returns `End` — an instant `Contains` reports as outside. |

### `RecurrenceScheduleTimeProviderExtensions` — `static class`

Six extensions, each forwarding `TimeProvider.GetUtcNow()` exactly once: `PreviousBoundary(timeProvider)`, `NextBoundary(timeProvider)`, `UntilNext(timeProvider)`, `CurrentWindow(timeProvider)`, `HasCrossed(lastSeen, timeProvider)`, `CountBoundaries(lastSeen, timeProvider)`. All throw `ArgumentNullException` for a null schedule or provider.

`WindowAt` and `EnumerateBoundaries` have **no** provider overload: they take a reference instant *plus* an explicit range, so one would save nothing.

## 2. Contracts (versioned / immutable)

### Boundary semantics — one rule

**A boundary instant belongs to the window it opens, not to the one it closes:** `CurrentWindow(b).Start == b` for every boundary `b`. Everything follows:

- `PreviousBoundary` is inclusive (`b <= asOf`), `NextBoundary` is strict (`b > asOf`), `CurrentWindow` is the half-open interval between them. Consecutive windows **tile the timeline exactly** — no instant is in two, none is in zero.
- `HasCrossed(lastSeen, now)` asks for a boundary in `(lastSeen, now]`: a `lastSeen` that is itself a boundary means that window has already been seen; a `now` that is exactly a boundary means it has just been crossed.
- `CountBoundaries` counts that same `(lastSeen, now]`; `HasCrossed` is exactly `CountBoundaries(...) > 0`, only cheaper.
- Because every comparison is between **instants**, the classic "compare the hour field" bug (treating 04:15 as past an 04:30 reset because `4 >= 4`) is unrepresentable through this API.
- `WindowAt(asOf, n).End == WindowAt(asOf, n + 1).Start` for every `n`.

### Daylight-saving contract — fixed for the lifetime of the type

The scheduled time is a **wall-clock** time in the schedule's zone. Three ways a wall clock misbehaves, three fixed resolutions:

1. **A scheduled time that does not exist** (clock jumps forward over it: 02:00→03:00 vs. an 02:30 schedule) → **the first valid instant after the gap**, i.e. the transition itself (03:00, not 03:30). The boundary is never dropped, so a daily schedule still has exactly one boundary that day and `CountBoundaries` still equals the number of elapsed days.
2. **A scheduled time that happens twice** (clock falls back over it: 02:00→01:00 vs. an 01:30 schedule) → the **first** occurrence, under the pre-transition (larger) UTC offset. The schedule does **not** fire twice.
3. **A scheduled time the wall clock never reaches**, because the zone's *base* offset changed permanently (Libya at the turn of 2012, Samoa's skipped 30 Dec 2011, re-based Russian zones) and `TimeZoneInfo.IsInvalidTime` reports nothing → **the first instant at which the zone's wall clock reaches the scheduled time**. Rule 1 is the special case where the zone itself calls the hole a gap.
4. Any other wall-clock time uses the zone's offset for that date, so the boundary stays at the intended local time year-round.

These rules are **a versioned contract**: boundaries get persisted, and comparing a stored instant against a recomputed one only works if the computation never moves. They will not change in a patch or minor release; a different resolution policy would ship as a new type. They are cadence-independent and hold for non-whole-hour shifts (`Australia/Lord_Howe` moves 30 minutes; a 02:15 schedule there resolves to the 02:30 transition).

### Complexity

| Member | Cost |
|---|---|
| `PreviousBoundary`, `NextBoundary`, `CurrentWindow`, `WindowAt`, `CountBoundaries`, `HasCrossed`, `UntilNext` | **O(1)** — closed-form calendar arithmetic; a ten-year gap costs what a one-day gap costs. Time is spent on `TimeZoneInfo` conversion, not on interval width (a UTC schedule is an order of magnitude cheaper). |
| `EnumerateBoundaries` | **O(number of boundaries)**, one zone resolution each. |
| Resolving a boundary landing on a gap or a base-offset seam (rules 1 and 3) | ~100× an ordinary resolution. One or two days a year per zone; never on the ordinary path. |

### Exceptions and edge cases

| Condition | Behaviour |
|---|---|
| `Weekly` with an undefined `DayOfWeek` | `ArgumentOutOfRangeException` |
| `Monthly` with `dayOfMonth` outside 1–31 | `ArgumentOutOfRangeException` |
| `new TimeWindow(start, end)` with `end < start` | `ArgumentException` |
| `new TimeWindow(start, start)` | Legal; contains nothing, overlaps nothing |
| `CountBoundaries` / `HasCrossed` / `EnumerateBoundaries` with `to <= from` | `0` / `false` / empty — never negative |
| `WindowAt` with an out-of-range `offset` | `ArgumentOutOfRangeException` — never a silently wrapped window |
| `Monthly(31, ...)` in February | Clamps to the 28th/29th; still exactly one boundary |
| A `TimeProvider` extension on a null schedule or provider | `ArgumentNullException` |
| An `asOf` within a boundary's distance of `DateTimeOffset.MinValue`/`MaxValue` | `ArgumentOutOfRangeException` from the underlying date arithmetic (deferred to the first `MoveNext` for `EnumerateBoundaries`) |

## 3. DO NOT

- **DO NOT use `DateTimeOffset.MinValue` (or `MaxValue`) as a "never seen" sentinel.** Boundaries are computed within `DateTime`'s range, so a persisted `lastSeen` of `MinValue` **throws** `ArgumentOutOfRangeException` rather than reporting every boundary since year 1. Store a real instant, or a `null` you check for.
- **DO NOT call `EnumerateBoundaries` when you only need the count.** `CountBoundaries` is O(1); the sequence resolves one time-zone instant per boundary (a decade of a daily schedule really is 3,653 resolutions). Bound the interval or `Take` before enumerating a wide one.
- **DO NOT assume `EnumerateBoundaries` validates its arguments eagerly.** There is nothing to validate — every pair of instants is meaningful — but execution is deferred, so a range-edge failure surfaces from the first `MoveNext`, not from the call.
- **DO NOT compare calendar fields (hour, day) to decide whether a reset has passed.** Use `HasCrossed`/`CountBoundaries`; the whole API compares instants precisely to make the hour-field bug unrepresentable.
- **DO NOT expect a boundary instant to belong to the window it closes.** It opens the next one: `yesterday.Contains(today.Start)` is `false`, `today.Contains(today.Start)` is `true`, and `yesterday.Overlaps(today)` is `false` even though `yesterday.End == today.Start`.
- **DO NOT look for an inclusive `TimeWindow` variant.** Half-open `[Start, End)` is the only containment rule; mixing inclusive and exclusive ends is what produces double counting at the shared endpoint in one method and a hole in another.
- **DO NOT expect `Clamp` to return an instant inside the window.** It clamps to the **closed** `[Start, End]`, so an overrun returns `End`, which `Contains` reports as outside. That is deliberate: it answers "how far into this window did we get".
- **DO NOT expect `UntilNext` ever to return `TimeSpan.Zero`.** `NextBoundary` is strict, so an `asOf` that is itself a boundary reports the full length of the window it just opened.
- **DO NOT parse, persist, or assert on `ToString()`.** It is a diagnostic rendering with no compatibility promise — unlike the DST rules.
- **DO NOT expect a DST-affected window to be exactly 24 hours.** Durations are real elapsed time: 23 h on spring-forward, 25 h on fall-back for a daily schedule in a one-hour zone.
- **DO NOT reach for the `TimeProvider` overloads to make code testable.** The core API already takes the instant as a parameter — that is what makes it a pure function. The extensions are sugar for callers that already hold an injected clock, and they only ever call `GetUtcNow()`.
- **DO NOT treat this as a scheduler.** It computes instants; it never runs anything. Quartz.NET / Hangfire / a hosted service still own execution.
- **DO NOT expect cron expressions, RFC 5545 rules, holiday calendars, open-ended intervals, or fixed-interval ("every 6 hours") recurrence.** Out of scope for v1: three calendar cadences only.
- **DO NOT assume the zone id resolves everywhere.** Ids follow `TimeZoneInfo`'s own resolution; IANA ids work on Windows from .NET 6 **provided ICU is available** — globalization-invariant mode is the case to watch.

## 4. Diagnostics

This package ships **no analyzer and no source generator**, so it defines no diagnostic ids. Every misuse surfaces as a runtime exception; see the table in §2.

## 5. Canonical snippets

### Daily reset with crossing detection

```csharp
using SsalKit.RecurrenceSchedule;

var seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), seoul);

if (dailyReset.HasCrossed(player.LastQuotaReset, now))
{
    player.Quota = DailyQuota;
    player.LastQuotaReset = dailyReset.PreviousBoundary(now);   // store a real instant, not MinValue
}

int missedRewards = dailyReset.CountBoundaries(player.LastLogin, now);   // O(1)
TimeSpan remaining = dailyReset.UntilNext(now);                          // always > TimeSpan.Zero
```

### Windows tile the timeline

```csharp
using SsalKit.RecurrenceSchedule;

TimeWindow today = dailyReset.CurrentWindow(now);
TimeWindow yesterday = dailyReset.WindowAt(now, -1);          // O(1) at any offset

bool meet = yesterday.End == today.Start;                     // true
bool overlap = yesterday.Overlaps(today);                     // false — touching is not overlapping
bool ownsStart = today.Contains(today.Start);                 // true
bool notYesterdays = yesterday.Contains(today.Start);         // false

TimeWindow? shared = today.Intersect(maintenanceWindow);      // null when disjoint
DateTimeOffset capped = today.Clamp(overrunInstant);          // == today.End
```

### Weekly and monthly, with the month-end clamp

```csharp
using SsalKit.RecurrenceSchedule;

var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));   // UTC by default
var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

DateTimeOffset feb = monthly.NextBoundary(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
// 2026-02-28T00:00:00+00:00 — February clamps and still gets exactly one boundary
```

### Daylight saving, both directions (America/New_York, 2026)

```csharp
using SsalKit.RecurrenceSchedule;

var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
var est = TimeSpan.FromHours(-5);
var edt = TimeSpan.FromHours(-4);

// Spring forward: 02:00 EST becomes 03:00 EDT, so 02:30 never happens -> the transition itself.
var spring = RecurrenceSchedule.Daily(new TimeOnly(2, 30), newYork);
DateTimeOffset springBoundary = spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, edt));
// 2026-03-08T03:00:00-04:00
TimeSpan springWindow = spring.CurrentWindow(new DateTimeOffset(2026, 3, 8, 12, 0, 0, edt)).Duration;
// 23:30 — the window shortens, the boundary survives

// Fall back: 01:30 happens twice -> the FIRST occurrence; it does not fire twice.
var autumn = RecurrenceSchedule.Daily(new TimeOnly(1, 30), newYork);
var first = new DateTimeOffset(2026, 11, 1, 1, 30, 0, edt);    // 05:30Z
var second = new DateTimeOffset(2026, 11, 1, 1, 30, 0, est);   // 06:30Z
DateTimeOffset autumnBoundary = autumn.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, est));
// == first
int firedTwice = autumn.CountBoundaries(first, second);        // 0
```

### With an injected clock

```csharp
using SsalKit.RecurrenceSchedule;

public sealed class QuotaService(TimeProvider timeProvider)
{
    private static readonly RecurrenceSchedule Reset =
        RecurrenceSchedule.Daily(new TimeOnly(4, 30), TimeZoneInfo.Utc);

    public bool ShouldRefill(DateTimeOffset lastReset) => Reset.HasCrossed(lastReset, timeProvider);

    public TimeSpan TimeLeft() => Reset.UntilNext(timeProvider);
}
```
