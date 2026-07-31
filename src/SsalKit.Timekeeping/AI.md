# SsalKit.Timekeeping — AI contract sheet

Deterministic, persistable time state computed without ever reading the clock itself: every member is a **pure function of `(state, instant)`** (or, for `TickSchedule`, `(state, tick)`), every state type is an immutable, serializable `record struct`. Three families: `RecurrenceSchedule` + `TimeWindow` (calendar wall-clock boundaries, with a permanently fixed daylight-saving contract), `Cooldown` + `RechargePool` (elapsed-time state — a single cooldown, or a capacity-bounded recharging pool), and `TickSchedule` (a deterministic, serializable queue of events due at logical simulation tick numbers — never a wall-clock reading). The first two families expose `TimeProvider` overloads for code that already holds a clock; `TickSchedule` deliberately does not (see §3).

- **TFM:** `net10.0`. **Package dependencies:** none (BCL only). No source generator, no analyzer.
- **Namespace:** `SsalKit.Timekeeping`. Formerly published as `SsalKit.RecurrenceSchedule` (deprecated) — the `RecurrenceSchedule`/`TimeWindow` types and contracts are unchanged; only the package id and namespace changed. `Cooldown`/`RechargePool` and `TickSchedule`/`TickScheduleEntry` are new in this package.
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 0. Which family do I need?

| Kind of boundary | Use |
|---|---|
| Calendar wall-clock (daily / weekly / monthly reset, DST) | `RecurrenceSchedule` |
| Elapsed time since an event (ability cooldown, stamina / charge pool) | `Cooldown` / `RechargePool` |
| Logical simulation tick (deterministic event dispatch by tick number, not by clock) | `TickSchedule` |
| In-process resource throttling (concurrent request limits, token buckets) | Not this package — `System.Threading.RateLimiting` |

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
| "Is this ability/action usable right now?" | `Cooldown.IsReady(asOf)` |
| "Use it now if possible" | `Cooldown.TryUse(asOf, out updated)` |
| "How many charges does a capped resource have right now?" | `RechargePool.AvailableAt(asOf)` |
| "Spend N charges if available" | `RechargePool.TryConsume(asOf, amount, out updated)` |
| "How long until the next charge / until completely full?" | `RechargePool.UntilNextCharge(asOf)` / `UntilFull(asOf)` |
| "Grant charges (reward, purchase) without exceeding capacity" | `RechargePool.Grant(amount, asOf)` |
| "Reset to fully charged at a calendar boundary" | `RechargePool.Refill(asOf)`, typically paired with `RecurrenceSchedule.HasCrossed` |
| Caller already holds an injected clock (Cooldowns) | the `CooldownTimeProviderExtensions` overloads |
| "Schedule an event for a future simulation tick" | `TickSchedule<TEvent>.Add(event, dueTick)` |
| "Which events are due now (or were missed while offline)?" | `TickSchedule<TEvent>.PopDue(currentTick, out updated)` |
| "Cancel a previously scheduled event" | `TickSchedule<TEvent>.RemoveAll(event)` |
| "How far can the simulation fast-forward before anything is due?" | `TickSchedule<TEvent>.NextDueTick` |

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

### `Cooldown` — `readonly record struct`

| Member | Contract |
|---|---|
| `static Cooldown Create(TimeSpan duration, DateTimeOffset asOf)` | Immediately usable (`ReadyAt = asOf`). `duration < 0` → `ArgumentOutOfRangeException`. `TimeSpan.Zero` is **legal** — a degenerate always-ready cooldown. |
| `bool IsReady(DateTimeOffset asOf)` | `asOf >= ReadyAt`. |
| `TimeSpan Remaining(DateTimeOffset asOf)` | `max(0, ReadyAt - asOf)`. Never negative. |
| `bool TryUse(DateTimeOffset asOf, out Cooldown updated)` | Success: `updated.ReadyAt = asOf + Duration`. Failure: `updated = this` — always safe to assign back. `ArgumentOutOfRangeException` if `asOf + Duration` overflows `DateTimeOffset`. |
| `Cooldown Reset(DateTimeOffset asOf)` | `ReadyAt = asOf`, discarding remaining wait. |
| `TimeSpan Duration { get; }` / `DateTimeOffset ReadyAt { get; }` | Configured wait length; instant the cooldown next becomes usable. |
| `default(Cooldown)` | **Legal.** `Duration = TimeSpan.Zero`, `ReadyAt = DateTimeOffset.MinValue` — behaves exactly like `Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue)`, always ready. No member guards against it. |

### `RechargePool` — `readonly record struct`

| Member | Contract |
|---|---|
| `static RechargePool Create(int capacity, TimeSpan rechargeEvery, DateTimeOffset asOf, int initialCharges = -1)` | `capacity >= 1`, `rechargeEvery > 0` → else `ArgumentOutOfRangeException`. `initialCharges` default `-1` means full; else must be `[0, capacity]`, else `ArgumentOutOfRangeException`. |
| `int AvailableAt(DateTimeOffset asOf)` | `0..Capacity`. `InvalidOperationException` if `default(RechargePool)`. |
| `bool TryConsume(DateTimeOffset asOf, int amount, out RechargePool updated)` | `amount < 1` → `ArgumentOutOfRangeException`. `amount > Capacity` → `ArgumentOutOfRangeException` (could never succeed — caller bug, not a permanent `false`). Insufficient charges: `false`, `updated = this`. Success: `updated.FullAt = max(FullAt, asOf) + amount * RechargeEvery`. |
| `TimeSpan? UntilNextCharge(DateTimeOffset asOf)` | `null` when full; else `> TimeSpan.Zero`. At most `RechargeEvery` while `asOf` is within the modeled recharge span (`FullAt - (Capacity - 1) * RechargeEvery` .. `FullAt`); for an earlier `asOf` (time reversal is total), it is the full duration to the earliest modeled charge and **can exceed `RechargeEvery`**. |
| `TimeSpan? UntilFull(DateTimeOffset asOf)` | `null` when full; else exactly `FullAt - asOf`. |
| `RechargePool Grant(int amount, DateTimeOffset asOf)` | `amount < 1` → `ArgumentOutOfRangeException`. No upper bound — over-granting saturates at `Capacity`. `updated.FullAt = max(asOf, FullAt - amount * RechargeEvery)`. |
| `RechargePool Refill(DateTimeOffset asOf)` | `FullAt = asOf`, discarding partial progress. Pairs with `RecurrenceSchedule.HasCrossed` for a calendar-triggered reset (see [Canonical snippets](#5-canonical-snippets)). |
| `int Capacity { get; }` / `TimeSpan RechargeEvery { get; }` / `DateTimeOffset FullAt { get; }` | Configured capacity and interval; the single instant the pool becomes completely full — the type's entire state (see §2). |
| `default(RechargePool)` | **Not legal.** `Capacity = 0`, `RechargeEvery = TimeSpan.Zero` — every member throws `InvalidOperationException`, including on a corrupted/truncated deserialized payload. |

### `CooldownTimeProviderExtensions` — `static class`

One overload per member above whose only "now"-shaped argument is `asOf`: `Cooldown.IsReady/Remaining/TryUse/Reset(timeProvider)`, `RechargePool.AvailableAt/TryConsume/UntilNextCharge/UntilFull/Grant/Refill(timeProvider, ...)`. Each forwards `TimeProvider.GetUtcNow()` exactly once. `ArgumentNullException` for a `null` provider.

### `TickSchedule<TEvent>` — `readonly record struct`

| Member | Contract |
|---|---|
| `static TickSchedule<TEvent> Empty { get; }` | The empty schedule. Identical to `default(TickSchedule<TEvent>)` — see §2. |
| `ImmutableArray<TickScheduleEntry<TEvent>> Entries { get; init; }` | Entries in storage (insertion-then-removal) order — the serialization surface together with `NextSequence`. Getter never returns a default `ImmutableArray` (normalizes to `.Empty`), even when the schedule itself is `default`. Carries no ordering meaning — see §2 determinism. |
| `long NextSequence { get; init; }` | The `Sequence` the next `Add` assigns. `0` on an empty schedule. |
| `int Count { get; }` | `Entries.Length`. |
| `bool IsEmpty { get; }` | `Entries.IsEmpty`. |
| `long? NextDueTick { get; }` | Smallest `DueTick` among entries, or `null` when empty. |
| `TickSchedule<TEvent> Add(TEvent event, long dueTick)` | Appends `new TickScheduleEntry<TEvent>(dueTick, NextSequence, event)`; `NextSequence` advances by one (checked). `dueTick` is any `long`, including at-or-before "now" (immediately due next `PopDue`). `OverflowException` if `NextSequence == long.MaxValue`. |
| `ImmutableArray<TickScheduleEntry<TEvent>> PopDue(long currentTick, out TickSchedule<TEvent> updated)` | Removes and returns every entry with `DueTick <= currentTick`, ordered `(DueTick, Sequence)` ascending — boundary **inclusive**. Empty result + `updated == this` (same value, not merely equal) when nothing is due — a total function, never throws for "nothing to pop". |
| `TickSchedule<TEvent> RemoveAll(TEvent event)` | Removes every entry with `Event` equal to `event` via `EqualityComparer<TEvent>.Default`. `NextSequence` unchanged — removed `Sequence` values are never reused. `updated == this` when nothing matched. |

### `TickScheduleEntry<TEvent>` — `readonly record struct(long DueTick, long Sequence, TEvent Event)`

| Member | Contract |
|---|---|
| `DueTick` | The logical tick this entry becomes due at. Opaque `long` — the library only ever compares it, never assigns meaning. |
| `Sequence` | Insertion order `Add` assigned; the FIFO tie-break for entries sharing a `DueTick`. Not the same as position in `Entries` — entries are never reordered in storage. |
| `Event` | The value passed to `Add`, handed back unexecuted by `PopDue`. No type constraint; a `notnull` type (enum, id, record) is recommended for predictable `RemoveAll`/serializer behavior. **Never a delegate** — see §3. |

## 2. Contracts (versioned / immutable)

### Boundary semantics — one rule (RecurrenceSchedule)

**A boundary instant belongs to the window it opens, not to the one it closes:** `CurrentWindow(b).Start == b` for every boundary `b`. Everything follows:

- `PreviousBoundary` is inclusive (`b <= asOf`), `NextBoundary` is strict (`b > asOf`), `CurrentWindow` is the half-open interval between them. Consecutive windows **tile the timeline exactly** — no instant is in two, none is in zero.
- `HasCrossed(lastSeen, now)` asks for a boundary in `(lastSeen, now]`: a `lastSeen` that is itself a boundary means that window has already been seen; a `now` that is exactly a boundary means it has just been crossed.
- `CountBoundaries` counts that same `(lastSeen, now]`; `HasCrossed` is exactly `CountBoundaries(...) > 0`, only cheaper.
- Because every comparison is between **instants**, the classic "compare the hour field" bug (treating 04:15 as past an 04:30 reset because `4 >= 4`) is unrepresentable through this API.
- `WindowAt(asOf, n).End == WindowAt(asOf, n + 1).Start` for every `n`.

### Boundary semantics — one rule (Cooldowns)

Same status as the rule above — a permanent, versioned contract, because the state is persisted:

> **A cooldown or a recharge unit is usable at the instant it completes, not only strictly after it.**

`cooldown.IsReady(cooldown.ReadyAt) == true`, `cooldown.Remaining(cooldown.ReadyAt) == TimeSpan.Zero`, `pool.AvailableAt(pool.FullAt) == Capacity` (not `Capacity - 1`).

### Determinism — one rule (TickSchedule)

Same permanent, versioned-contract status as the two rules above:

> **Dispatch order is `(DueTick ascending, Sequence ascending)`, and nothing else, regardless of the order `Entries` happens to be stored in.**

- `PopDue`'s boundary is **inclusive**: an entry due at tick `N` is returned by `PopDue(N, ...)`, not only by `PopDue(N + 1, ...)` — consistent with the package-wide "a boundary belongs to the thing it opens" convention.
- `Add` never sorts `Entries`; all ordering happens inside `PopDue`. There is no sorted-storage invariant to maintain, so there is none for a deserializer (or a hand-edited/corrupted payload) to violate — `PopDue` honors the rule above for `Entries` in **any** order.
- `default(TickSchedule<TEvent>)` is **legal** — `Entries` default-normalizes to empty, `NextSequence` is `0`, identical to `Empty`. No `EnsureValid` guard exists because no invalid state exists.
- If a corrupted payload produces duplicate `Sequence` values or a regressed `NextSequence`, `PopDue` stays fully deterministic by breaking any remaining tie with each entry's storage position as an implementation-only third sort key — the *observable* contract stays `(DueTick, Sequence)` for any payload `Add` could legitimately have produced.
- `Entries` + `NextSequence` are the entire serialization surface (both public `init`); STJ round-trips with no custom converter, and dispatch order after restore matches the order before saving regardless of the order the deserializer reconstructs `Entries` in.

### `RechargePool` state = `FullAt` (the O(1) source)

The entire state is `FullAt`, a single instant. Every other quantity is derived:

```
available(t)  = Capacity - clamp(ceil((FullAt - t) / RechargeEvery), 0, Capacity)
consume(k, t) : FullAt' = max(FullAt, t) + k * RechargeEvery
grant(k, t)   : FullAt' = max(t, FullAt - k * RechargeEvery)
refill(t)     : FullAt' = t
untilNext(t)  = null if full else (FullAt - (missing - 1) * RechargeEvery) - t
```

This is a **permanent, versioned contract**, the same status as `RecurrenceSchedule`'s daylight-saving resolution rules: `FullAt` is persisted, so re-deriving `AvailableAt` from a stored value only works if the formula never changes. Properties that follow, all pinned by tests:

- **Partial progress toward the next unit is preserved exactly.** Consuming pushes `FullAt` forward by one `RechargeEvery` from `max(FullAt, asOf)`, never resetting a charge already pending. Whether a matching `Grant` (same amount, same instant) restores the *original* `FullAt` depends on whether a charge was pending at consume time: if `FullAt >= asOf` (pending), the round trip is exact, even for observations before that instant. If the pool was already full (`FullAt <= asOf`), the round trip instead lands `FullAt` on the consume/grant instant rather than the earlier instant it actually became full at — `AvailableAt`/`UntilNextCharge`/`UntilFull` still agree from that instant onward (both report full throughout), but a query from before it, or comparing the two `RechargePool` values for equality, tells them apart.
- **An offline gap is O(1) regardless of length** — a ten-year gap costs what a one-minute gap costs.
- **Time going backwards is total, not exceptional.** No stored "last observed instant" exists to violate; an earlier `asOf` simply reports fewer available units via the `clamp` term, never an exception or corrupted state.
- Boundary inclusion is consistent with §2's Cooldowns rule.

### Daylight-saving contract — fixed for the lifetime of the type (RecurrenceSchedule)

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
| Every `Cooldown` and `RechargePool` member | **O(1)** — arithmetic on `ReadyAt`/`FullAt` only, independent of elapsed time or offline duration; no benchmark project (scheduling API, not a hot path — same rationale as `RecurrenceSchedule`). |
| `TickSchedule<TEvent>.Add` | **O(n)** in the current entry count — copies `Entries`' backing array (`ImmutableArray<T>.Add`'s usual cost). |
| `TickSchedule<TEvent>.PopDue` | **O(n + k log k)** — `n` = total entries (full scan to select the due subset), `k` = due entries (sort of only that subset). |
| `TickSchedule<TEvent>.RemoveAll` / `Count` / `IsEmpty` / `NextDueTick` | **O(n)**. |

Both `TickSchedule` costs are for game-/simulation-scale entry counts (hundreds to low thousands); the public contract is storage-order independence, not this specific representation, so a future release could change the internal layout without breaking callers.

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
| `Cooldown.Create`/`RechargePool.Create`: `duration < 0` / `capacity < 1` / `rechargeEvery <= 0` / `initialCharges` outside `[0, capacity]` (and not `-1`) | `ArgumentOutOfRangeException` |
| `RechargePool.TryConsume`/`Grant`: `amount < 1` | `ArgumentOutOfRangeException` |
| `RechargePool.TryConsume`: `amount > Capacity` | `ArgumentOutOfRangeException` — never a permanent `false` |
| `RechargePool.TryConsume`: valid `amount`, insufficient charges | `false`, `updated = this` |
| `default(Cooldown)` (incl. corrupted/truncated deserialized payload) | **Legal** — behaves like `Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue)` |
| `default(RechargePool)` (incl. corrupted/truncated deserialized payload) | Every member throws `InvalidOperationException` |
| `Cooldown`/`RechargePool` with `asOf` earlier than a previously used instant | No exception — see "time going backwards" in §2 |
| `Cooldown`/`RechargePool` arithmetic outside `DateTimeOffset`'s range, or `RechargePool`'s tick multiplication overflow | `ArgumentOutOfRangeException` / `OverflowException` from checked arithmetic |
| `TickSchedule<TEvent>.Add`: `NextSequence` would overflow past `long.MaxValue` | `OverflowException` (checked arithmetic) — not a practical concern at any real event rate |
| `TickSchedule<TEvent>.PopDue`: nothing due | Not an error — empty result array, `updated == this`; a total function |
| `default(TickSchedule<TEvent>)` (incl. corrupted/truncated deserialized payload) | **Legal** — behaves like `TickSchedule<TEvent>.Empty`; every member operates normally, no guard needed |
| `TickSchedule<TEvent>` payload with duplicate `Sequence` values or a regressed `NextSequence` | No exception — `PopDue` stays deterministic via the storage-position tie-break (§2) |

## 3. DO NOT

- **DO NOT use `DateTimeOffset.MinValue` (or `MaxValue`) as a `RecurrenceSchedule` "never seen" sentinel.** Boundaries are computed within `DateTime`'s range, so a persisted `lastSeen` of `MinValue` **throws** `ArgumentOutOfRangeException` rather than reporting every boundary since year 1. Store a real instant, or a `null` you check for. (`Cooldown`'s `default`/`MinValue`-derived state is the opposite case — see below.)
- **DO NOT call `EnumerateBoundaries` when you only need the count.** `CountBoundaries` is O(1); the sequence resolves one time-zone instant per boundary (a decade of a daily schedule really is 3,653 resolutions). Bound the interval or `Take` before enumerating a wide one.
- **DO NOT assume `EnumerateBoundaries` validates its arguments eagerly.** There is nothing to validate — every pair of instants is meaningful — but execution is deferred, so a range-edge failure surfaces from the first `MoveNext`, not from the call.
- **DO NOT compare calendar fields (hour, day) to decide whether a reset has passed.** Use `HasCrossed`/`CountBoundaries`; the whole API compares instants precisely to make the hour-field bug unrepresentable.
- **DO NOT expect a boundary instant to belong to the window it closes.** It opens the next one: `yesterday.Contains(today.Start)` is `false`, `today.Contains(today.Start)` is `true`, and `yesterday.Overlaps(today)` is `false` even though `yesterday.End == today.Start`.
- **DO NOT look for an inclusive `TimeWindow` variant.** Half-open `[Start, End)` is the only containment rule; mixing inclusive and exclusive ends is what produces double counting at the shared endpoint in one method and a hole in another.
- **DO NOT expect `Clamp` to return an instant inside the window.** It clamps to the **closed** `[Start, End]`, so an overrun returns `End`, which `Contains` reports as outside. That is deliberate: it answers "how far into this window did we get".
- **DO NOT expect `UntilNext` ever to return `TimeSpan.Zero`.** `NextBoundary` is strict, so an `asOf` that is itself a boundary reports the full length of the window it just opened.
- **DO NOT parse, persist, or assert on `ToString()`.** It is a diagnostic rendering with no compatibility promise — unlike the DST rules.
- **DO NOT expect a DST-affected window to be exactly 24 hours.** Durations are real elapsed time: 23 h on spring-forward, 25 h on fall-back for a daily schedule in a one-hour zone.
- **DO NOT reach for the `TimeProvider` overloads (either family) to make code testable.** The core APIs already take the instant as a parameter — that is what makes them pure functions. The extensions are sugar for callers that already hold an injected clock, and they only ever call `GetUtcNow()`.
- **DO NOT treat this package as a scheduler.** It computes instants and state; it never runs anything. Quartz.NET / Hangfire / a hosted service still own execution.
- **DO NOT treat `Cooldown`/`RechargePool` as an in-process rate limiter.** They model persisted, cross-restart state compared against a specific stored instant — not concurrent request throttling. Use `System.Threading.RateLimiting` (`TokenBucketRateLimiter`, `ConcurrencyLimiter`) for that; it solves a different problem and nothing in it needs to survive a restart.
- **DO NOT treat `default(RechargePool)` as usable.** Unlike `Cooldown`, whose `default` is a legal always-ready value, `RechargePool.Capacity = 0` and `RechargeEvery = TimeSpan.Zero` are not something the formula in §2 can evaluate; every member throws `InvalidOperationException`, including on a corrupted/truncated deserialized payload. Always construct via `RechargePool.Create`.
- **DO NOT assume `TryConsume`/`Grant`/`TryUse` are safe against concurrent read-modify-write.** These types are immutable and thread-safe to *read*, but `if (pool.TryConsume(now, 1, out var updated)) player.Stamina = updated;` still races if two threads run it against the same stored value at once. The package adds no locking; that is the caller's responsibility, the same as any optimistic-concurrency update.
- **DO NOT expect cron expressions, RFC 5545 rules, holiday calendars, open-ended intervals, or fixed-interval ("every 6 hours") recurrence.** Out of scope for v1: three calendar cadences only.
- **DO NOT assume the zone id resolves everywhere.** Ids follow `TimeZoneInfo`'s own resolution; IANA ids work on Windows from .NET 6 **provided ICU is available** — globalization-invariant mode is the case to watch.
- **DO NOT treat `TickSchedule` as an execution engine.** It only ever tells you what is due; dispatching `entry.Event` (a `switch`, a lookup table, a queue push) is entirely the caller's job, the same as `RecurrenceSchedule` only ever answering *when*.
- **DO NOT store a delegate as `TEvent`.** Callbacks cannot survive a serialize/deserialize round trip the way a value can — store an enum, an id, or a record, and look it up or switch on it at dispatch time.
- **DO NOT compare two `TickSchedule<TEvent>` values with `==` expecting "same logical schedule".** Equality is sensitive to storage order (`Entries` in a different order compares unequal even when `PopDue` would treat the schedules identically) and, independently, to `ImmutableArray<T>`'s own identity-based equality (two schedules built by separately `Add`-ing the same entries in the same order can still compare unequal). Compare `PopDue` results, or `Entries` converted to a plain list, instead.
- **DO NOT look for a `TimeProvider` overload on `TickSchedule`.** There is none, and there will not be one — a logical tick is not a wall-clock reading. Advance `currentTick` from whatever the simulation already uses to count ticks.
- **DO NOT assume `TickSchedule<TEvent>` serializes `TEvent` for you beyond what your serializer already handles.** The schedule only guarantees `Entries` + `NextSequence` round-trip through STJ/etc. with no custom converter *for the schedule itself*; `TEvent` still needs to be a type your chosen serializer can handle (an enum, primitive id, or `[JsonSerializable]`-annotated record — the same responsibility as serializing any other application type).
- **DO NOT expect `TickSchedule` to reorder `Entries` for you, or to maintain a sorted invariant.** Storage is deliberately append-only; only `PopDue` sorts, and only the due subset, at the moment it is called.

## 4. Diagnostics

This package ships **no analyzer and no source generator**, so it defines no diagnostic ids. Every misuse surfaces as a runtime exception; see the table in §2.

## 5. Canonical snippets

### Daily reset with crossing detection

```csharp
using SsalKit.Timekeeping;

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
using SsalKit.Timekeeping;

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
using SsalKit.Timekeeping;

var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));   // UTC by default
var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

DateTimeOffset feb = monthly.NextBoundary(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
// 2026-02-28T00:00:00+00:00 — February clamps and still gets exactly one boundary
```

### Daylight saving, both directions (America/New_York, 2026)

```csharp
using SsalKit.Timekeeping;

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
using SsalKit.Timekeeping;

public sealed class QuotaService(TimeProvider timeProvider)
{
    private static readonly RecurrenceSchedule Reset =
        RecurrenceSchedule.Daily(new TimeOnly(4, 30), TimeZoneInfo.Utc);

    public bool ShouldRefill(DateTimeOffset lastReset) => Reset.HasCrossed(lastReset, timeProvider);

    public TimeSpan TimeLeft() => Reset.UntilNext(timeProvider);
}
```

### Cooldowns: a single ability, and a capped resource

```csharp
using SsalKit.Timekeeping;

// A single ability on a 30-second cooldown.
var cooldown = Cooldown.Create(TimeSpan.FromSeconds(30), now);

if (cooldown.TryUse(now, out var updated))
{
    player.AbilityCooldown = updated;   // save this back to storage
}

// Five stamina charges, recharging one every 20 minutes.
var pool = RechargePool.Create(capacity: 5, rechargeEvery: TimeSpan.FromMinutes(20), asOf: now);

if (pool.TryConsume(now, amount: 1, out var updatedPool))
{
    player.Stamina = updatedPool;       // save this back too
}

int available = pool.AvailableAt(now);
TimeSpan? untilNext = pool.UntilNextCharge(now);
```

### Combining `RecurrenceSchedule` with `RechargePool`

The two families are orthogonal — neither type knows about the other — so a calendar-triggered pool reset is ordinary calling code:

```csharp
using SsalKit.Timekeeping;

var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

if (dailyReset.HasCrossed(player.LastStaminaReset, now))
{
    var boundary = dailyReset.PreviousBoundary(now);
    player.Stamina = player.Stamina.Refill(boundary);   // preserves boundary-instant inclusion (§2)
    player.LastStaminaReset = boundary;
}
```

### TickSchedule: a tick loop, with catch-up after a restart

```csharp
using SsalKit.Timekeeping;

// Persisted (or reloaded from a save): world.Schedule is TickSchedule<string>, world.LastTick a long.
for (long tick = world.LastTick + 1; tick <= currentSimulationTick; tick++)
{
    var due = world.Schedule.PopDue(tick, out world.Schedule);   // boundary inclusive (§2)

    foreach (var entry in due)
    {
        Dispatch(entry.Event);   // caller decides what "boss-respawn" etc. means and does it
    }
}

world.LastTick = currentSimulationTick;

// A process that fell behind (or just restarted) does not need to replay every intervening tick --
// one PopDue at the caught-up tick returns every entry due at or before it, in (DueTick, Sequence)
// order, exactly as if each intervening PopDue had been called in turn.
var missed = world.Schedule.PopDue(currentSimulationTick, out world.Schedule);

// Recurring events (v1 has no built-in repeat): re-Add for the next occurrence as each one pops.
foreach (var entry in missed)
{
    if (entry.Event is "wave")
    {
        world.Schedule = world.Schedule.Add(entry.Event, entry.DueTick + WaveInterval);
    }
}
```
