[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ja.md)

# SsalKit.Timekeeping

Formerly published as `SsalKit.RecurrenceSchedule` (deprecated). Same types, same contracts — only the package id and namespace changed.

SsalKit.Timekeeping computes deterministic, persistable time state without ever reading the clock itself: every member is a pure function of `(state, instant)`, every state type is an immutable, serializable `record struct`, and the instant is always a parameter you supply — directly, or via a `TimeProvider` overload for code that already holds a clock. Zero dependencies.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Timekeeping.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Timekeeping)

| Component | Answers | Status |
|---|---|---|
| [`RecurrenceSchedule`](#quick-start-recurrenceschedule) + [`TimeWindow`](#timewindow-one-containment-rule) | Calendar wall-clock boundaries — daily / weekly / monthly resets, with a permanently fixed daylight-saving contract | Original |
| [`Cooldown`](#quick-start-cooldowns) + [`RechargePool`](#cooldowns) | Elapsed-time state — a single cooldown, or a capacity-bounded recharging pool | New |

### Where the boundary is

| Kind of boundary | Use |
|---|---|
| Calendar wall-clock (daily / weekly / monthly reset, DST) | `RecurrenceSchedule` |
| Elapsed time since an event (ability cooldown, stamina / charge pool) | `Cooldown` / `RechargePool` |
| In-process resource throttling (concurrent request limits, token buckets) | Not this package — see [`System.Threading.RateLimiting`](https://learn.microsoft.com/dotnet/api/system.threading.ratelimiting) |

## Why SsalKit.Timekeeping?

"Has the daily reset happened since the last time we looked?" turns up in every codebase with a daily quota, a login reward, a billing period, or a reporting window. It looks like two lines of `DateTime` arithmetic — which is exactly why it gets rewritten at each call site instead of shared, and why the copies end up disagreeing.

The prototype this library was extracted from had two of them side by side:

- **One hardcoded to midnight UTC and Monday**, reading `DateTime.UtcNow` inside its own methods, so nothing built on it could be tested without moving the machine clock. Its containment rule also changed from method to method: one included both endpoints, another compared calendar dates.
- **Another supporting a configurable reset time**, which decided "has the reset passed" with `from.Hour >= resetHour`. So 04:15 counted as past an 04:30 reset, because `4 >= 4` — every minute and second of the configured schedule was silently discarded, and the bug only showed up as players getting a second daily reward at a quarter past four.

Two different answers to one question, in one codebase, with 25-plus call sites and persisted "last reset" fields between them.

.NET 8 added `TimeProvider`, which settles the "who owns the clock" half of the problem. What it does not add is a type for the recurring window itself — the BCL still has no notion of "the reset period this instant belongs to". NodaTime models calendars and time zones in depth but has no reset-window concept; Cronos parses cron expressions and gives you the next occurrence, not window membership or crossing counts.

SsalKit.Timekeeping fills that gap:

- **`RecurrenceSchedule`** defines a calendar-aligned recurrence — every day at 04:30 Seoul time, every Monday at 09:00 UTC, the 31st of every month — and answers the four questions worth asking about it: `PreviousBoundary`, `NextBoundary`, `CurrentWindow`, and, for the "player was away" case, `HasCrossed` / `CountBoundaries`.
- **One containment rule, everywhere.** `TimeWindow` is half-open `[Start, End)` with no inclusive variant, so consecutive windows tile the timeline with neither double counting nor holes.
- **A daylight-saving contract that is fixed for the lifetime of the type.** Boundaries get persisted, so the resolution of a wall-clock time that is skipped, repeated, or never reached at all is a versioned promise, not an implementation detail.
- **Everything is a pure function of `(schedule, instant)`.** Nothing reads the ambient clock. The `TimeProvider` overloads are sugar on top, not the other way round.
- **Zero dependencies.** No `PackageReference`, BCL only.

## Installation

```bash
dotnet add package SsalKit.Timekeeping
```

## Quick Start: RecurrenceSchedule

```csharp
using SsalKit.Timekeeping;

var seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), seoul);

// Has the 04:30 reset happened since we last looked?
if (dailyReset.HasCrossed(player.LastQuotaReset, now))
{
    player.Quota = DailyQuota;
    player.LastQuotaReset = dailyReset.PreviousBoundary(now);
}

// How many daily rewards did a returning player miss? (Boundaries in (lastSeen, now].)
int missedRewards = dailyReset.CountBoundaries(player.LastLogin, now);

// Which reset period are we in, and how long is left in it?
TimeWindow today = dailyReset.CurrentWindow(now);
TimeSpan remaining = dailyReset.UntilNext(now);   // always strictly positive
```

`WindowAt` reaches a neighbouring period by offset — `0` is today, `-1` is the one before it — which is what a "compared to yesterday" figure needs, and `EnumerateBoundaries` walks the boundaries of an interval lazily and in order:

```csharp
TimeWindow yesterday = dailyReset.WindowAt(now, -1);   // O(1); -30 costs the same

foreach (var boundary in dailyReset.EnumerateBoundaries(player.LastLogin, now))
{
    // exactly CountBoundaries(player.LastLogin, now) of them, ascending, in (lastSeen, now]
}
```

Weekly and monthly cadences work the same way, and a monthly schedule anchored past the end of a short month clamps to its last day:

```csharp
var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));   // UTC by default
var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

monthly.NextBoundary(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
// 2026-02-28T00:00:00+00:00 -- February clamps, and still gets exactly one boundary
```

A runnable walkthrough of all of this, daylight saving included, is in [samples/SsalKit.Timekeeping.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Timekeeping.Sample).

## API Overview: RecurrenceSchedule

### `RecurrenceSchedule`

| Member | Purpose |
|---|---|
| `Daily(TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | Once every calendar day at the given wall-clock time. `timeZone` defaults to UTC. |
| `Weekly(DayOfWeek dayOfWeek, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | Once every calendar week, on the given day. |
| `Monthly(int dayOfMonth, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | Once every calendar month, on day `1`–`31`; shorter months clamp to their last day. |
| `PreviousBoundary(DateTimeOffset asOf)` | The greatest boundary `b <= asOf`. Returns `asOf` itself when `asOf` is a boundary. |
| `NextBoundary(DateTimeOffset asOf)` | The least boundary `b > asOf`. Strictly after, so a boundary returns the following one. |
| `UntilNext(DateTimeOffset asOf)` | `NextBoundary(asOf) - asOf`. Always **strictly positive**, so a boundary reports a whole window rather than zero. |
| `CurrentWindow(DateTimeOffset asOf)` | `[PreviousBoundary(asOf), NextBoundary(asOf))` — the reset period `asOf` belongs to. |
| `WindowAt(DateTimeOffset asOf, int offset)` | The window `offset` periods away: `0` is `CurrentWindow(asOf)`, `-1` the previous one. O(1). |
| `HasCrossed(DateTimeOffset lastSeen, DateTimeOffset now)` | Whether some boundary `b` satisfies `lastSeen < b <= now`. |
| `CountBoundaries(DateTimeOffset lastSeen, DateTimeOffset now)` | How many such `b` there are; `0` when `now <= lastSeen`. |
| `EnumerateBoundaries(DateTimeOffset from, DateTimeOffset to)` | Those same boundaries themselves, ascending and lazily. Exactly `CountBoundaries(from, to)` of them. |
| `ToString()` | A diagnostic rendering — `Daily 04:30 @ UTC`, `Weekly Monday 09:00 @ Asia/Seoul`, `Monthly day 31 00:00 @ America/New_York`. |

Boundaries always come back carrying **the schedule time zone's UTC offset for that date** — `+09:00` for a Seoul schedule, `-05:00` or `-04:00` for a New York one, `+00:00` for a UTC one. Comparisons are unaffected (`DateTimeOffset` compares instants), but formatting a boundary shows the local wall-clock time the schedule was written in.

Three notes on the members added for convenience:

- **`CountBoundaries` is O(1); `EnumerateBoundaries` is O(number of boundaries).** They cover the same half-open interval `(from, to]` and the sequence always has exactly as many elements as the count, but the count is closed-form calendar arithmetic while the sequence resolves each boundary in turn. Ask for the count when the count is all you need. The sequence is deferred — there are no arguments to validate, so nothing is checked early either — and it can be cut short with `Take` or bounded by a wide `to`.
- **`WindowAt` never wraps.** An `offset` that would move the window outside the range of `DateTime` throws `ArgumentOutOfRangeException` rather than silently landing in some unrelated century; the arithmetic is done in 64 bits and range-checked.
- **`ToString()` is for logs, not for parsing.** Unlike the daylight-saving rules below, the format carries no compatibility promise and may be improved in any release. It is rendered with the invariant culture and the zone's `TimeZoneInfo.Id`, and the time of day grows to `HH:mm:ss` (or full tick precision) only when the schedule is that precise.

### `TimeWindow`

A `readonly record struct` for the half-open interval `[Start, End)`.

| Member | Purpose |
|---|---|
| `new TimeWindow(DateTimeOffset start, DateTimeOffset end)` | `start == end` is a legal empty window; `start > end` throws `ArgumentException`. |
| `Start` / `End` / `Duration` | Inclusive start, exclusive end, and `End - Start` (never negative). |
| `Contains(DateTimeOffset instant)` | `Start <= instant < End`. Always `false` for an empty window. |
| `Overlaps(TimeWindow other)` | Whether the intersection is non-empty. Windows that merely touch do **not** overlap. |
| `Intersect(TimeWindow other)` | The shared interval, or `null`. Symmetric. |
| `Clamp(DateTimeOffset instant)` | Restricts to the *closed* range `[Start, End]`, so an overrun clamps to `End`. |

### `TimeProvider` extensions

`RecurrenceScheduleTimeProviderExtensions` mirrors the six members whose whole argument is "now", forwarding `TimeProvider.GetUtcNow()` — read exactly once per call, so nothing can tear against a moving clock:

| Extension | Equivalent to |
|---|---|
| `schedule.PreviousBoundary(timeProvider)` | `schedule.PreviousBoundary(timeProvider.GetUtcNow())` |
| `schedule.NextBoundary(timeProvider)` | `schedule.NextBoundary(timeProvider.GetUtcNow())` |
| `schedule.UntilNext(timeProvider)` | `schedule.UntilNext(timeProvider.GetUtcNow())` |
| `schedule.CurrentWindow(timeProvider)` | `schedule.CurrentWindow(timeProvider.GetUtcNow())` |
| `schedule.HasCrossed(lastSeen, timeProvider)` | `schedule.HasCrossed(lastSeen, timeProvider.GetUtcNow())` |
| `schedule.CountBoundaries(lastSeen, timeProvider)` | `schedule.CountBoundaries(lastSeen, timeProvider.GetUtcNow())` |

`WindowAt` and `EnumerateBoundaries` have no provider overload: they take a reference instant *plus* an explicit range, so one would save nothing over passing `timeProvider.GetUtcNow()` yourself.

`TimeProvider` is part of the BCL from .NET 8 onward, so these add no package dependency.

## Boundary semantics: RecurrenceSchedule

Everything follows from one rule: **a boundary instant belongs to the window it opens, not to the one it closes.**

```csharp
schedule.CurrentWindow(b).Start == b   // for any boundary b
```

Which makes the rest fall out:

- `PreviousBoundary` is inclusive (`b <= asOf`), `NextBoundary` is strict (`b > asOf`), and `CurrentWindow` is the half-open interval between them. Consecutive windows therefore tile the timeline exactly — no instant is in two windows, and none is in zero.
- `HasCrossed(lastSeen, now)` asks whether a boundary sits in the **half-open interval `(lastSeen, now]`**. If `lastSeen` is itself a boundary, that window has already been seen and nothing has been crossed. If `now` is exactly a boundary, it has just been crossed.
- `CountBoundaries` counts the boundaries in that same `(lastSeen, now]`, and `HasCrossed` is exactly `CountBoundaries(...) > 0` — just cheaper, since it stops at the first one. A reversed interval (`now < lastSeen`) counts `0` rather than going negative.

Because every comparison is between instants rather than between calendar fields, the prototype's hour-comparison bug is not merely fixed but **unrepresentable**:

```csharp
var reset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

reset.HasCrossed(At(4, 00), At(4, 15));   // false -- an hour-field comparison would say true
reset.HasCrossed(At(4, 00), At(4, 30));   // true
```

## Daylight-saving contract

The scheduled time is a **wall-clock** time in the schedule's time zone, and a wall clock can misbehave in exactly three ways. Every resolution is pinned here:

1. **A scheduled time that does not exist** — the clock jumps forward over it, as 02:00 → 03:00 does to an 02:30 schedule — moves to **the first valid instant after the gap**: the transition itself, 03:00, not 03:30. The boundary is never dropped, so a daily schedule still has exactly one boundary that day and `CountBoundaries` still equals the number of elapsed days.
2. **A scheduled time that happens twice** — the clock falls back over it, as 02:00 → 01:00 does to an 01:30 schedule — resolves to the **first** occurrence, the one under the pre-transition (larger) UTC offset. The schedule does not fire twice that day.
3. **A scheduled time the wall clock never reaches**, because the zone's *base* offset changed permanently rather than seasonally — Libya at the turn of 2012, Venezuela in 2007, Samoa's skipped 30 December 2011, North Korea in 2015, several re-based Russian zones — resolves to **the first instant at which the zone's wall clock reaches the scheduled time**. This is rule 1's principle stated in full, and rule 1 is the special case of it where the zone itself calls the hole a gap: `TimeZoneInfo.IsInvalidTime` reports nothing at these seams, and the offset the zone gives for the local time is not the offset in force at the instant that pairing points at. Resolving by "first instant reaching" is well defined even where the wall clock runs backwards for an hour before jumping forwards, which is what a seam makes it do.
4. **Every other wall-clock time** uses the zone's offset for that date, so the boundary stays at the intended local time year-round instead of drifting with the seasons.

Whether a given zone carries a seam is a property of the platform's time-zone data rather than of this library — the histories above are visible in the data Windows ships and are recorded as ordinary transitions by a tzdata build — so rule 3 is about being correct either way, not about a particular zone.

Worked from the 2026 US transitions, using `America/New_York`:

```csharp
// Spring forward: 2026-03-08, 02:00 EST becomes 03:00 EDT, so 02:30 never happens.
var spring = RecurrenceSchedule.Daily(new TimeOnly(2, 30), newYork);

spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, Est));   // 2026-03-07T02:30:00-05:00
spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));  // 2026-03-08T03:00:00-04:00  <- the transition
spring.NextBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));      // 2026-03-09T02:30:00-04:00
spring.CurrentWindow(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)).Duration;  // 23:30 -- the window shortens, the boundary survives

// Fall back: 2026-11-01, 02:00 EDT becomes 01:00 EST, so 01:30 happens twice.
var autumn = RecurrenceSchedule.Daily(new TimeOnly(1, 30), newYork);
var first  = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt);   // 05:30Z
var second = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Est);   // 06:30Z

autumn.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est));  // == first
autumn.CountBoundaries(first, second);                                    // 0 -- it does not fire twice
autumn.CurrentWindow(second).Duration;                                    // 25:00 -- the window lengthens
```

A seam, worked from the data Windows ships for `Africa/Tripoli`, where the base offset drops from +02:00 to +01:00 at the turn of 2012:

```csharp
// 2011-12-31T21:00Z reads 23:00, 22:00Z reads 23:00 again (the base offset dips), and 23:00Z
// reads 01:00 -- so the wall clock never reads 2012-01-01 00:00 at all.
var midnight = RecurrenceSchedule.Daily(new TimeOnly(0, 0), tripoli);

midnight.PreviousBoundary(new DateTimeOffset(2012, 1, 1, 12, 0, 0, TimeSpan.FromHours(2)));
// 2012-01-01T01:00:00+02:00, i.e. 2011-12-31T23:00Z -- the first instant to reach the scheduled time
```

These rules are **a versioned contract, never changed in a patch or minor release.** Boundaries get persisted — "the reset this player last saw" — and comparing a stored instant against a recomputed one only works if the computation never moves. Exactly like the algorithm contract of a seeded PRNG: if a different resolution policy is ever needed, it ships as a new type rather than as new behaviour on this one.

The rules are also cadence-independent — a weekly or monthly schedule whose boundary lands on a transition day resolves identically — and they hold for zones whose shift is not a whole hour (`Australia/Lord_Howe` moves by 30 minutes, and a 02:15 schedule there resolves to the 02:30 transition, not to 02:45).

Time zone identifiers follow `TimeZoneInfo`'s own resolution. On .NET 6 and later, `TimeZoneInfo.FindSystemTimeZoneById` accepts IANA identifiers such as `America/New_York` on Windows as well as Unix, provided ICU is available; a Windows machine running in globalization-invariant mode is the case to watch for.

## `TimeWindow`: one containment rule

Half-open `[Start, End)` is the only rule this type offers, and there is deliberately no inclusive variant. A codebase that mixes them gets double counting at the shared endpoint from one pair of methods and a hole at the same endpoint from another — which is precisely how the prototype ended up with two disagreeing answers.

```csharp
var yesterday = dailyReset.WindowAt(now, -1);

yesterday.End == today.Start;          // true  -- they meet exactly
yesterday.Overlaps(today);             // false -- touching is not overlapping
yesterday.Contains(today.Start);       // false -- the shared instant belongs to today
today.Contains(today.Start);           // true

today.Intersect(maintenanceWindow);    // the shared interval, or null
today.Clamp(overrunInstant);           // == today.End: "how far into this window did we get"
```

**Offsets are display only.** `DateTimeOffset` denotes a point on the timeline, and both `TimeWindow`'s operations and its value equality compare those points. `2026-07-25T04:30:00+09:00` and `2026-07-24T19:30:00+00:00` are the same instant, so windows written either way are `==` and behave identically; the offsets are carried through into `Start`, `End` and `ToString()` purely so a window produced by a schedule still shows its zone's local time.

## Cooldowns

`Cooldown` and `RechargePool` answer a different question from `RecurrenceSchedule`: not "has a calendar boundary passed" but "how much elapsed time stands between now and the next unit becoming usable". Both are `readonly record struct`s whose entire state is one or two `DateTimeOffset` / `TimeSpan` fields, so they survive a process restart or an offline gap exactly the way `RecurrenceSchedule`'s persisted boundaries do — store the struct, and re-derive everything else from it and whatever instant you ask about later.

### Quick Start: Cooldowns

```csharp
using SsalKit.Timekeeping;

// A single ability on a 30-second cooldown.
var cooldown = Cooldown.Create(TimeSpan.FromSeconds(30), now);

if (cooldown.TryUse(now, out var updated))
{
    player.AbilityCooldown = updated;   // save this back to storage
}

TimeSpan left = cooldown.Remaining(now);
bool ready = cooldown.IsReady(now);

// Five stamina charges, recharging one every 20 minutes.
var pool = RechargePool.Create(capacity: 5, rechargeEvery: TimeSpan.FromMinutes(20), asOf: now);

if (pool.TryConsume(now, amount: 1, out var updatedPool))
{
    player.Stamina = updatedPool;       // save this back too
}

int available = pool.AvailableAt(now);
TimeSpan? untilNext = pool.UntilNextCharge(now);
```

Both types are pure functions of `(state, instant)` — nothing here reads the ambient clock — so `player.AbilityCooldown` and `player.Stamina` round-trip through storage (JSON, a database column, whatever) exactly as written; the next `IsReady` / `AvailableAt` call re-derives everything from the stored struct and whichever instant you pass it, with no separate "last saved at" bookkeeping needed.

### API Overview: Cooldown and RechargePool

#### `Cooldown`

| Member | Purpose |
|---|---|
| `static Cooldown Create(TimeSpan duration, DateTimeOffset asOf)` | An immediately-usable cooldown; `duration` is how long a future `TryUse` puts it into. `duration < 0` throws `ArgumentOutOfRangeException`; `TimeSpan.Zero` is legal and produces a cooldown that is always ready. |
| `IsReady(DateTimeOffset asOf)` | `asOf >= ReadyAt`. |
| `Remaining(DateTimeOffset asOf)` | `max(0, ReadyAt - asOf)`. Never negative. |
| `TryUse(DateTimeOffset asOf, out Cooldown updated)` | On success, starts a fresh `Duration`-long wait (`ReadyAt = asOf + Duration`). On failure, `updated` is this instance unchanged — assigning it back is always safe. |
| `Reset(DateTimeOffset asOf)` | Immediately usable again at `asOf`, discarding any remaining wait. |
| `Duration` / `ReadyAt` | The configured wait length, and the instant the cooldown next becomes usable at. |

#### `RechargePool`

| Member | Purpose |
|---|---|
| `static RechargePool Create(int capacity, TimeSpan rechargeEvery, DateTimeOffset asOf, int initialCharges = -1)` | `capacity >= 1`, `rechargeEvery > 0` — else `ArgumentOutOfRangeException`. `initialCharges` defaults to `-1`, meaning full; any other value must be in `[0, capacity]`. |
| `AvailableAt(DateTimeOffset asOf)` | A value in `0..Capacity`. |
| `TryConsume(DateTimeOffset asOf, int amount, out RechargePool updated)` | `amount` must be `1..Capacity` (`amount > Capacity` throws — a request this pool could never satisfy). Returns `false` (and leaves `updated` unchanged) when fewer than `amount` units are currently available. |
| `UntilNextCharge(DateTimeOffset asOf)` | `null` when full. Otherwise, if `asOf` falls within the pool's modeled recharge span (`FullAt - (Capacity - 1) * RechargeEvery` up to `FullAt`), at most `RechargeEvery` long; if `asOf` is earlier than that span, the full duration until the earliest modeled charge completes, which can exceed `RechargeEvery`. |
| `UntilFull(DateTimeOffset asOf)` | `null` when full; otherwise exactly `FullAt - asOf`. |
| `Grant(int amount, DateTimeOffset asOf)` | Adds units, clamped at `Capacity`. Unlike `TryConsume`, `amount` has no upper bound — over-granting just saturates the pool. |
| `Refill(DateTimeOffset asOf)` | Completely full at `asOf`, discarding any partial progress toward the next unit. |
| `Capacity` / `RechargeEvery` / `FullAt` | The configured capacity and recharge interval, and the single instant the pool becomes completely full — see [The `FullAt` model](#the-fullat-model). |

#### `CooldownTimeProviderExtensions`

The same pattern as `RecurrenceScheduleTimeProviderExtensions`: one overload per member above whose only "now"-shaped argument is `asOf` (`IsReady`, `Remaining`, `TryUse`, `Reset` on `Cooldown`; `AvailableAt`, `TryConsume`, `UntilNextCharge`, `UntilFull`, `Grant`, `Refill` on `RechargePool`), each forwarding `TimeProvider.GetUtcNow()` exactly once. `ArgumentNullException` for a `null` provider.

### Boundary semantics: Cooldowns

One rule, and it carries the same permanent, versioned-contract status as `RecurrenceSchedule`'s "a boundary belongs to the window it opens":

> **A cooldown or a recharge unit is usable at the instant it completes, not only strictly after it.**

```csharp
cooldown.IsReady(cooldown.ReadyAt);       // true
cooldown.Remaining(cooldown.ReadyAt);     // TimeSpan.Zero
pool.AvailableAt(pool.FullAt);            // Capacity, not Capacity - 1
```

Cooldown and pool state is persisted the same way a `RecurrenceSchedule` boundary is — "the instant this ability was last used", "the instant this pool will be full" — so this comparison can never change meaning between releases without breaking a stored `ReadyAt` or `FullAt`.

### The `FullAt` model

`RechargePool`'s entire state is `FullAt` — the single instant the pool becomes completely full. Every other quantity is derived from it and `RechargeEvery`:

```
available(t)  = Capacity - clamp(ceil((FullAt - t) / RechargeEvery), 0, Capacity)
consume(k, t) : FullAt' = max(FullAt, t) + k * RechargeEvery
grant(k, t)   : FullAt' = max(t, FullAt - k * RechargeEvery)
refill(t)     : FullAt' = t
```

This is what makes every member **O(1)** regardless of how long the pool has been offline or how many units are missing, and it gives the type three properties worth relying on:

- **Partial progress toward the next unit is preserved exactly.** Consuming a unit pushes `FullAt` forward by one `RechargeEvery` from whichever is later of `FullAt` and the consume instant, so it never resets progress toward a charge that was already pending. Whether a matching `Grant` (same amount, same instant) undoes that shift back to the *original* `FullAt` depends on whether a charge was actually pending: if it was (`FullAt` at or after the consume instant), the round trip is lossless, including for observations made before that instant. If the pool was already full instead (`FullAt` at or before the consume instant), the round trip lands `FullAt` on the consume/grant instant itself rather than on the earlier instant the pool had actually become full at — observations at or after that instant still agree (both states report the pool full throughout), but a query from before it, or an equality comparison of the two `RechargePool` values, can tell them apart.
- **An offline gap costs the same whether it lasted a minute or ten years.** `AvailableAt` after any gap is one subtraction and one division, never a loop over missed recharges.
- **Time going backwards is handled, not thrown.** There is no stored "last observed instant" to violate: an `asOf` earlier than one used before simply reports fewer available units (via the `clamp` term above), never an exception and never a corrupted state.

### Exceptions

| Condition | Behavior |
|---|---|
| `Create`: `capacity < 1` / `rechargeEvery <= 0` / `duration < 0` / `initialCharges` outside its legal range | `ArgumentOutOfRangeException` |
| `TryConsume` / `Grant`: `amount < 1` | `ArgumentOutOfRangeException` |
| `TryConsume`: `amount > Capacity` | `ArgumentOutOfRangeException` — a pool this size could never hold enough units to satisfy the request, so it is rejected as a caller bug rather than returning `false` forever |
| `TryConsume`: a valid `amount`, but fewer units currently available | `false`, `updated` unchanged — assigning it back over the original is always safe |
| `default(Cooldown)` (including a corrupted or truncated deserialized payload) | Legal — behaves exactly like `Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue)`, i.e. always ready |
| `default(RechargePool)` (including a corrupted or truncated deserialized payload) | Every member throws `InvalidOperationException` — unlike `Cooldown`, a capacity-`0` / never-recharging pool is not a usable degenerate state |
| A time going backwards | No exception — see "time going backwards" above |
| Arithmetic outside the range of `DateTimeOffset`, or overflowing `RechargePool`'s tick multiplication | `ArgumentOutOfRangeException` (or `OverflowException`) from the underlying checked arithmetic |

`Cooldown` and `RechargePool` diverge on their default value on purpose: `Cooldown.Duration = TimeSpan.Zero` is already a legal "no cooldown configured" degenerate case, so `default(Cooldown)` is simply that case pre-built. `RechargePool.Create` would need to divide by a zero `RechargeEvery` to make sense of the all-zero default, so instead every member guards against it explicitly and throws.

### Serialization and thread safety

Both types are `record struct`s with only public `get`/`init` properties, so System.Text.Json (or MessagePack, or anything else) round-trips them with no custom converter. The struct *is* the state: `Cooldown` stores two fields (`Duration`, `ReadyAt`), `RechargePool` three (`Capacity`, `RechargeEvery`, `FullAt`). A constructor bypass from deserializing a corrupted payload is caught by the guards in the exceptions table above at the point a method is called, not at deserialization time.

Immutable values plus pure functions make both types safe to share across threads for reads. What they do not do is make a read-modify-write sequence atomic: `if (pool.TryConsume(now, 1, out var updated)) player.Stamina = updated;` still races if two threads run it against the same stored value concurrently. That is the same responsibility a caller already has around any optimistic-concurrency update, and this package does not add locking on top of it.

### Combining with `RecurrenceSchedule`

The two families are orthogonal — neither type knows about the other — which is what makes "reset this pool every day at 04:30, but let it recharge normally the rest of the time" ordinary calling code rather than a feature either type needs to provide:

```csharp
using SsalKit.Timekeeping;

var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

if (dailyReset.HasCrossed(player.LastStaminaReset, now))
{
    var boundary = dailyReset.PreviousBoundary(now);
    player.Stamina = player.Stamina.Refill(boundary);
    player.LastStaminaReset = boundary;
}
```

## Testing

Because the core API takes the instant as an argument, most tests need no clock at all — just pass the instant you want to test. Where a class under test holds an injected `TimeProvider`, hand it a fake:

```csharp
// Microsoft.Extensions.TimeProvider.Testing, or a few lines of your own:
sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
{
    private readonly DateTimeOffset _utcNow = instant.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 25, 9, 15, 0, TimeSpan.FromHours(9)));

Assert.True(dailyReset.HasCrossed(lastReset, clock));
Assert.Equal(5, dailyReset.CountBoundaries(lastLogin, clock));
```

The extension methods only ever call `GetUtcNow()`, so there is nothing else to fake. `Cooldown` and `RechargePool` test the same way — pass the instant directly, or hand the same fake `TimeProvider` to their extension methods.

## Performance

`CountBoundaries`, `PreviousBoundary`, `NextBoundary`, `CurrentWindow` and `WindowAt` are **O(1)**: closed-form calendar arithmetic, not loops. A ten-year gap costs what a one-day gap costs, so counting missed rewards for an account dormant since 2020 is not a 3,653-iteration walk, and `WindowAt(now, -1000)` costs what `WindowAt(now, -1)` costs. All of them land in the **low microseconds**, and what they spend it on is `TimeZoneInfo` conversion rather than the width of the interval — a UTC schedule, which needs no conversion at all, is an order of magnitude cheaper than the same call in a zone with daylight saving.

Two things are not O(1) and are not meant to be:

- `EnumerateBoundaries` is O(number of boundaries), one time-zone resolution each — reach for `CountBoundaries` when the count is all that is wanted.
- Resolving a boundary that falls on a daylight-saving gap or a base-offset seam (rules 1 and 3) searches for the instant the wall clock reaches the scheduled time, which costs perhaps a hundred times an ordinary resolution. That is one or two days a year per zone, and it never touches the ordinary path.

Every `Cooldown` and `RechargePool` member is likewise O(1) — the [`FullAt` model](#the-fullat-model) above is precisely what makes that true regardless of how long a pool has been offline.

**No benchmark project ships with this library**, deliberately: unlike `SsalKit.Randomness`, this is a scheduling API rather than a hot path, and pinning absolute numbers to a machine would be a promise worth less than the maintenance it costs. The complexity claims above are what the library commits to; they are asserted structurally by the test suite rather than by a wall-clock budget.

## Where this sits

- **Not a scheduler.** This library computes instants; it never runs anything. Quartz.NET, Hangfire, or a hosted service still own execution — ask a `RecurrenceSchedule` *when*, and let them do the *doing*.
- **Not a resource limiter.** `Cooldown` and `RechargePool` model state that gets persisted and compared against a specific instant — a player's ability cooldown, a login-reward pool. `System.Threading.RateLimiting` (`TokenBucketRateLimiter`, `ConcurrencyLimiter`, and friends) solves a different problem: throttling concurrent, in-process work where nothing needs to survive a restart or be compared across processes. Reach for a `RateLimiter` for API throttling; reach for these types when the state itself needs to be stored, inspected, or restored.
- **Complementary to NodaTime.** NodaTime models calendar systems, periods, and zoned arithmetic far more thoroughly than the BCL. It has no "reset window" concept, and this library has no ambition to replace it: if you already use NodaTime for calendar work, this still answers the crossing questions, on BCL types, with no dependency to reconcile.
- **Complementary to Cronos.** Cronos parses cron expressions and gives you the next occurrence. This library does not parse cron — it offers three fixed calendar cadences instead — but it does answer what a cron parser does not: which window an instant belongs to, and how many occurrences fall between two instants.

Out of scope for v1, deliberately: cron expressions, RFC 5545 recurrence rules, business-day and holiday calendars, open-ended intervals, and fixed-interval ("every 6 hours") recurrence, whose anchoring rules are a separate design problem.

## Edge cases and exceptions: RecurrenceSchedule and TimeWindow

| Condition | Behaviour |
|---|---|
| `Weekly` with an undefined `DayOfWeek` | `ArgumentOutOfRangeException` |
| `Monthly` with `dayOfMonth` outside 1–31 | `ArgumentOutOfRangeException` |
| `new TimeWindow(start, end)` with `end` before `start` | `ArgumentException` |
| `new TimeWindow(start, start)` | Legal. An empty window contains nothing and overlaps nothing. |
| `CountBoundaries` / `HasCrossed` / `EnumerateBoundaries` with `to <= from` | `0` / `false` / an empty sequence — never negative |
| `WindowAt` with an `offset` that leaves the representable range | `ArgumentOutOfRangeException` — never a silently wrapped window |
| `Monthly(31, ...)` in February | Clamps to the 28th or 29th; the month still gets exactly one boundary |
| A `TimeProvider` extension called on a `null` schedule or provider | `ArgumentNullException` |

**One caution at the extremes of the range.** Boundaries are computed within the range of `DateTime`. An `asOf` within a boundary's distance of `DateTimeOffset.MinValue` or `MaxValue` asks for a boundary that is not representable, and the underlying date arithmetic throws `ArgumentOutOfRangeException`. Sentinel values such as `DateTimeOffset.MinValue` for "never seen" are therefore worth avoiding — a persisted `lastSeen` of `MinValue` will throw rather than report every boundary since the year 1. Store a real instant, or a `null` you check for. `Cooldown` and `RechargePool` have their own exception table under [Cooldowns](#cooldowns) — notably, `Cooldown` treats `default`/`MinValue`-derived state as legal rather than throwing, the opposite of `RecurrenceSchedule`'s caution here.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
