# SsalKit.Randomness — AI contract sheet

Deterministic, state-serializable PRNG (`xoshiro256**` + SplitMix64) + `IRandomSource` abstraction + weighted sampling, with a bundled source generator for selector-less weighted picking.

- **TFM:** `net10.0`. **Package dependencies:** none (BCL only).
- **Bundled analyzer:** `SsalKit.Randomness.Generator` (`netstandard2.0`) ships inside the package under `analyzers/dotnet/cs`. No separate package.
- **Namespace:** `SsalKit.Randomness` (all public types).
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 1. API surface

### Pick the right type

| Requirement | Use |
|---|---|
| Reproducible sequence from a seed; save/restore | `new DeterministicRandom(ulong seed)` + `ExportState()`/`FromState()` |
| Reproducible, but seed must be unguessable | `DeterministicRandom.CreateRandomlySeeded()` |
| Security-sensitive (tokens, secrets, secret shuffles) | `CryptoRandomSource.Instance` — never `DeterministicRandom` |
| Throwaway randomness, multithreaded, no replay | `SharedRandomSource.Instance` |
| Wrap an existing `Random` (interop/tests) | `new SystemRandomSource(random)` |
| Independent child stream per entity/subsystem | `parent.Fork()` |
| One weighted draw | `source.PickWeighted(items, selectorOrWeights)` |
| Many weighted draws from a **fixed** table | build `WeightedSampler<T>` once, then `Pick`/`PickMany` |
| `n` distinct weighted draws | `source.PickManyWeightedDistinct(items, selector, n)` |
| Drop the repeated weight selector | `[RandomWeight]` on the weight member (generator writes the selector) |

### `DeterministicRandom` — `sealed class : IRandomSource`

| Member | Contract |
|---|---|
| `DeterministicRandom(ulong seed)` | Expands `seed` through SplitMix64 into the 256-bit state. Every `ulong`, including `0`, is valid. |
| `static DeterministicRandom FromState(RandomState state)` | Resumes the exact sequence that followed `state`. Throws `ArgumentException` for the all-zero state. |
| `static DeterministicRandom CreateRandomlySeeded()` | Seed drawn from `RandomNumberGenerator`. The instance itself stays predictable. |
| `RandomState ExportState()` | The full 256-bit state; persistable. |
| `DeterministicRandom Fork()` | Exactly `new DeterministicRandom(this.NextUInt64())`. Advances `this` by one draw. |
| `ulong NextUInt64()` | Raw xoshiro256\*\* output, full `ulong` range. |
| `int Next()` | `[0, int.MaxValue)`. `int.MaxValue` is never returned. |
| `int Next(int maxValue)` | `[0, maxValue)`; returns `0` when `maxValue == 0`; `ArgumentOutOfRangeException` when negative. |
| `int Next(int minValue, int maxValue)` | `[minValue, maxValue)`; returns `minValue` when equal; `ArgumentOutOfRangeException` when `min > max`. |
| `long NextInt64()` / `(long max)` / `(long min, long max)` | Same contracts at 64 bits. `NextInt64()` is `[0, long.MaxValue)`. |
| `double NextDouble()` | `[0, 1)`, 53 bits. `1.0` never returned. |
| `float NextSingle()` | `[0, 1)`, 24 bits. `1.0f` never returned. |
| `bool NextBoolean()` | MSB of one `NextUInt64()` draw; `p = 0.5`. |
| `void NextBytes(Span<byte> buffer)` | Fills 8 bytes per draw, little-endian; a trailing partial chunk takes the low bytes of one more draw. Empty span allowed. |

### `RandomState` — `readonly record struct (ulong S0, ulong S1, ulong S2, ulong S3)`

| Member | Contract |
|---|---|
| `bool IsValid()` | **A method, not a property.** `true` when any word is non-zero. Deliberately a method so serializers do not emit it. |
| `ulong[] ToArray()` | New 4-element array in `[S0, S1, S2, S3]` order. |
| `void CopyTo(Span<ulong> destination)` | Non-allocating copy; `ArgumentException` when `destination.Length < 4`. |
| `static RandomState FromSpan(ReadOnlySpan<ulong> source)` | `ArgumentException` when `source.Length < 4` **or** the result is all-zero. |

Round-trips losslessly through `System.Text.Json` with no converter.

### `IRandomSource` and its implementations

| Member | Contract |
|---|---|
| `interface IRandomSource` | `ulong NextUInt64()`, `void NextBytes(Span<byte>)`. Everything else is an extension method over these two. |
| `CryptoRandomSource.Instance` | Singleton over `RandomNumberGenerator.Fill`. Unpredictable, thread-safe, not seedable, not reproducible. |
| `SharedRandomSource.Instance` | Singleton over `Random.Shared`. Thread-safe, not seedable, not reproducible. |
| `new SystemRandomSource(Random random)` | Adapter; `ArgumentNullException` on null. Thread safety follows the wrapped instance. |

### `RandomSourceExtensions` — extensions on `IRandomSource`

`Next()`, `Next(int maxValue)`, `Next(int minValue, int maxValue)`, `NextInt64()`, `NextInt64(long maxValue)`, `NextInt64(long minValue, long maxValue)`, `NextDouble()`, `NextSingle()`, `NextBoolean()` — signatures, semantics, and draw order identical to `DeterministicRandom`'s instance methods, so both routes produce the same sequence from the same state.

| Member | Contract |
|---|---|
| `void Shuffle<T>(Span<T> values)` | In-place Fisher–Yates. |
| `void Shuffle<T>(IList<T> values)` | In-place Fisher–Yates; `ArgumentNullException` on null list. |
| `T Pick<T>(ReadOnlySpan<T> items)` | Uniform. `ArgumentException` when empty. |
| `T Pick<T>(IReadOnlyList<T> items)` | Uniform. `ArgumentNullException`/`ArgumentException`. |

### `WeightedRandomExtensions`

| Member | Contract |
|---|---|
| `T PickWeighted<T>(this IRandomSource, IReadOnlyList<T> items, Func<T, long> weight)` | Exact integer arithmetic. `O(n)` cumulative sum + `O(log n)` search. |
| `T PickWeighted<T>(this IRandomSource, IReadOnlyList<T> items, Func<T, double> weight)` | 53-bit resolution — see Contracts §2. |
| `T PickWeighted<T>(this IRandomSource, ReadOnlySpan<T> items, ReadOnlySpan<long> weights)` | Parallel-span form. Allocation-free up to 256 items, heap buffer beyond. |
| `T PickWeighted<T>(this IRandomSource, ReadOnlySpan<T> items, ReadOnlySpan<double> weights)` | Same, `double` weights. |
| `T[] PickManyWeighted<T>(this IRandomSource, IReadOnlyList<T> items, Func<T, long> weight, int count)` | With replacement. Builds the cumulative sum once; identical draw order to a `PickWeighted` loop. |
| `T[] PickManyWeightedDistinct<T>(this IRandomSource, IReadOnlyList<T> items, Func<T, long> weight, int count)` | Without replacement, in selection order. `O(count * n)`. `count` must not exceed the number of **strictly positive**-weight items. |
| `WeightedSampler<T> ToWeightedSampler<T>(this IReadOnlyList<T> items, Func<T, long> weight)` | The only member here that extends the collection instead of the source, so `T` is inferred. Forwards to `WeightedSampler<T>.Create`. |

### `WeightedSampler<T>` — `sealed class`, immutable, thread-safe

| Member | Contract |
|---|---|
| `static WeightedSampler<T> Create(IReadOnlyList<T> items, Func<T, long> weight)` | `O(n)` Walker/Vose alias table, exact integer arithmetic (`Int128` intermediates). |
| `static WeightedSampler<T> Create(ReadOnlySpan<T> items, ReadOnlySpan<long> weights)` | `ArgumentException` when lengths differ. |
| `int Count` | Number of items the sampler was built from. |
| `T Pick(IRandomSource source)` | `O(1)`; exactly two bounded draws from `source`. |
| `T[] PickMany(IRandomSource source, int count)` | `count` repetitions of `Pick`. `ArgumentOutOfRangeException` when `count <= 0`. |

### `RandomWeightAttribute` — `[AttributeUsage(Property | Field, AllowMultiple = false, Inherited = false)]`

| Property | Default | Effect |
|---|---|---|
| `bool InternalExtensions` | `false` | Forces the generated extension class `internal` even for a `public` declaring type. No effect when the type is already `internal` or narrower (the class is capped at the type's effective accessibility either way). |
| `bool SharedSourceOverloads` | `false` | Additionally generates argument-less overloads drawing from `SharedRandomSource.Instance`. **Opt-in; unreproducible by construction.** |

Generated per decorated type, into that type's own namespace, receiver `IReadOnlyList<T>`:

| Weight member type | Generated |
|---|---|
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long` | `PickWeighted(source)`, `PickManyWeighted(source, count)`, `PickManyWeightedDistinct(source, count)`, `ToWeightedSampler()` |
| `float`, `double` | `PickWeighted(source)` only |
| anything else | nothing — `SSALR001` |

## 2. Contracts (versioned / immutable)

- **Algorithm contract (v1), permanently fixed for `DeterministicRandom`:** output sequence is `xoshiro256**`; seed expansion is SplitMix64; state is exactly four `ulong` words exposed as `RandomState`. The same seed or the same restored state produces the same sequence on any platform, in any process, forever. This will not change in a patch or minor release; algorithmic evolution ships as a **new type** (e.g. `DeterministicRandomV2`), never as changed behaviour here.
- **All-zero state is invalid.** `xoshiro256**` never leaves it. `FromState` and `RandomState.FromSpan` reject it with `ArgumentException`; `RandomState.IsValid()` reports it.
- **`Fork()` contract:** exactly `new DeterministicRandom(this.NextUInt64())`. The parent advances by exactly one draw, indistinguishable from any other `NextUInt64()`. Children are seeded from 64 bits, so collisions become non-negligible only around `2^32` forks — not provably disjoint streams.
- **Bias-free ranges.** All ranged draws use Lemire multiply-shift-reject, not `%`.
- **Instance vs. extension parity.** `DeterministicRandom`'s instance methods and `RandomSourceExtensions` funnel through the same bounded-draw helper, so the same starting state yields the same sequence either way.
- **`long` weights are exact; `double` weights are not.** A `double`-weighted draw takes `total * NextDouble()` (53-bit mantissa): an item whose weight is below roughly `total / 2^53` can be unreachable, and cumulative summation rounds on top. `WeightedSampler<T>`'s alias table is exact integer arithmetic.
- **Weight-0 items are legal** and never selected; only the total must be positive.
- **`DeterministicRandom` is predictable by design.** A handful of consecutive outputs reveals the state. Never for tokens, credentials, or secret shuffles.

### Exception contract (uniform across ranged draws, weighted APIs, and `RandomState`)

| Condition | Exception |
|---|---|
| `items` empty | `ArgumentException` |
| negative weight | `ArgumentException` (names the index) |
| `double` weight is `NaN`/`Infinity` | `ArgumentException` (names the index) |
| total weight `0` | `ArgumentException` |
| `long` weight sum overflows | `OverflowException` (checked summation) |
| `count <= 0` | `ArgumentOutOfRangeException` |
| `PickManyWeightedDistinct` `count` > positive-weight item count | `ArgumentOutOfRangeException` |
| all-zero state to `FromState`/`FromSpan` | `ArgumentException` |
| `minValue > maxValue` on a ranged draw | `ArgumentOutOfRangeException` |
| `maxValue < 0` on `Next(max)`/`NextInt64(max)` | `ArgumentOutOfRangeException` |
| null source / items / selector | `ArgumentNullException` |

### Thread safety

| Type | Thread-safe |
|---|---|
| `DeterministicRandom` | **No.** Concurrent use corrupts state and destroys reproducibility. One per thread, or synchronize. |
| `CryptoRandomSource` | Yes (singleton). |
| `SharedRandomSource` | Yes (singleton). |
| `SystemRandomSource` | Follows the wrapped `Random`. `new Random(seed)` is not thread-safe. |
| `WeightedSampler<T>` | Yes — immutable after `Create`; the mutable state lives in the caller's `IRandomSource`. |

## 3. DO NOT

- **DO NOT build a `WeightedSampler<T>` inside a draw loop.** `Create`/`ToWeightedSampler()` is `O(n)`; only `Pick` is `O(1)`. Build once per table, hold it (immutable, thread-safe), draw repeatedly. For a single draw call `PickWeighted` instead.
- **DO NOT assume `PickManyWeightedDistinct` gives inclusion probabilities proportional to weight.** Only the first draw is; every later draw renormalizes over the remaining items. For `count > 1`, light items are over-represented relative to `count * weight / total`. This is successive sampling, not πps; the library implements no πps design.
- **DO NOT use `double` weights when the ratio between largest and smallest positive weight is extreme.** Use the `long` overloads (or `WeightedSampler<T>`) — see the 53-bit limit in §2.
- **DO NOT expect `ulong` or `decimal` weights to work with `[RandomWeight]`.** Both are rejected with `SSALR001` — `ulong`→`long` can overflow, and no runtime overload accepts `decimal`. Same for enums, nullable numerics, and non-numeric types.
- **DO NOT write `[property: RandomWeight]` on a positional record parameter or `[field: RandomWeight]` on an auto-property.** Target-redirecting forms are never seen by the generator: nothing is generated and **no diagnostic is reported**. Declare a plain property or field (`public long Weight { get; init; }`).
- **DO NOT expect argument-less `list.PickWeighted()` to exist by default.** `SharedSourceOverloads` is opt-in per type, and turning it on makes those draws unreproducible (the shared source is not seedable). Leave it off wherever draws must be replayable.
- **DO NOT share a `DeterministicRandom` across threads.** It is the one non-thread-safe type here. `WeightedSampler<T>` is safe to share; give each thread its own source.
- **DO NOT write `state.IsValid` — it is `IsValid()`, a method.**
- **DO NOT use `DeterministicRandom` for anything security-sensitive**, including "shuffle this deck and keep the order secret". Use `CryptoRandomSource`.
- **DO NOT expect `[RandomWeight]` on a base type to generate extensions for derived types.** Inheritance is not walked; the generated receiver is `IReadOnlyList<TheDeclaringType>` (usable from `List<Derived>` via covariance, but the result's static type is the base).
- **DO NOT pass a lazy sequence to a generated extension.** Every generated receiver is `IReadOnlyList<T>`; materialize with `.ToList()` first.
- **DO NOT apply `[RandomWeight]` to more than one member of a type** (`SSALR002`), to a `static`/write-only/indexer member (`SSALR003`), or inside a generic type or `ref struct` (`SSALR005`/`SSALR006`).

## 4. Diagnostics

Prefix `SSALR`, category `SsalKit.Randomness`. All six are **errors**, and when any fires for a type, **no extension class is generated for that type at all** (no partial generation).

| ID | Trigger | Fix |
|---|---|---|
| `SSALR001` | `[RandomWeight]` member's type is not a supported weight type (`ulong`, `decimal`, enum, nullable numeric, non-numeric). | Use `sbyte`/`byte`/`short`/`ushort`/`int`/`uint`/`long` for the full set, or `float`/`double` for `PickWeighted` only. |
| `SSALR002` | A type declares more than one `[RandomWeight]` member (reported once per offending member). | Keep exactly one; move extra weights to their own types. |
| `SSALR003` | The member is `static`, write-only, or an indexer. | Make it a readable instance property or field. |
| `SSALR004` | The member, its declaring type, or a containing type is not reachable from the generated top-level class (`private`, `protected`, `private protected`, or `file`-local). | Make the whole chain at least `internal` and not file-local. |
| `SSALR005` | The declaring type is generic or nested inside a generic type. | Use a concrete type, or call the selector-based runtime overloads directly. |
| `SSALR006` | The declaring type is a `ref struct`. | Use a normal class/struct — a `ref struct` cannot be a generic type argument. |

## 5. Canonical snippets

### Seed, save, restore, fork

```csharp
using SsalKit.Randomness;

var rng = new DeterministicRandom(seed: 42);

int roll = rng.Next(1, 7);            // [1, 7)
double chance = rng.NextDouble();     // [0, 1)

RandomState saved = rng.ExportState();          // persist this (JSON, blob, row)
DeterministicRandom resumed = DeterministicRandom.FromState(saved);

DeterministicRandom perEntity = rng.Fork();     // independent child stream
```

### Weighted picking, three shapes

```csharp
using SsalKit.Randomness;

var rng = new DeterministicRandom(seed: 42);
string[] items = ["common", "rare", "legendary"];
long[] weights = [80, 18, 2];

// Single shot, parallel spans (allocation-free up to 256 items).
string drop = rng.PickWeighted(items.AsSpan(), weights.AsSpan());

// Repeated draws from a fixed table: build the alias table ONCE, outside the loop.
WeightedSampler<string> sampler = WeightedSampler<string>.Create(items, weights.AsSpan());
for (int i = 0; i < 1000; i++)
{
    string pick = sampler.Pick(rng);            // O(1)
}

// Items that carry their own weight: T is inferred from the receiver.
(string Name, long Weight)[] loot = [("common", 80), ("rare", 18), ("legendary", 2)];
WeightedSampler<(string Name, long Weight)> lootSampler = loot.ToWeightedSampler(entry => entry.Weight);
```

### `[RandomWeight]` — plain property, explicit source

```csharp
using SsalKit.Randomness;

namespace Game.Loot;

public sealed class LootEntry
{
    public required string ItemId { get; init; }

    [RandomWeight]                       // plain property; NOT [property: RandomWeight]
    public long Weight { get; init; }
}

public static class Drops
{
    public static LootEntry Roll(IReadOnlyList<LootEntry> table, IRandomSource source)
        => table.PickWeighted(source);   // generated into namespace Game.Loot
}
```

### Writing against the abstraction

```csharp
using SsalKit.Randomness;

// Works with DeterministicRandom, SharedRandomSource.Instance, CryptoRandomSource.Instance,
// or SystemRandomSource — the extension surface is identical for all of them.
public static T ChooseOne<T>(IRandomSource source, IReadOnlyList<T> candidates)
    => source.Pick(candidates);

public static void ShuffleInPlace<T>(IRandomSource source, IList<T> values)
    => source.Shuffle(values);
```
