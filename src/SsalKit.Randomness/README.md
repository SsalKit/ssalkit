[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ja.md)

# SsalKit.Randomness

A deterministic, state-serializable PRNG (xoshiro256** + SplitMix64) with a unified random-source abstraction and weighted-random sampling. Zero dependencies.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness)

## Why SsalKit.Randomness?

Game logic, simulations, and procedural content all eventually run into the same requirement: given the same seed or the same saved state, the exact same sequence of "random" outcomes must come out again — for replays, for deterministic lockstep multiplayer, for save files that reproduce a run bit-for-bit.

`System.Random` doesn't quite get you there:

- **Seeded `Random` uses a legacy algorithm.** The `int`-seeded constructor keeps its output stable for compatibility, but that stability was never a first-class design goal, and there's no guarantee across all `Random` construction paths.
- **No state export.** `System.Random` has no supported way to pull out its internal state, persist it, and restore it later — you either keep the `Random` instance alive for the whole run, or you lose reproducibility.
- **No stream-splitting.** There's no built-in way to derive an independent, reproducible child generator from a parent (useful for per-entity or per-subsystem randomness that still traces back to one root seed).

SsalKit.Randomness takes a different approach:

- **`DeterministicRandom`** is a sealed, `System.Random`-shaped PRNG (xoshiro256**) whose full 256-bit state can be exported, persisted anywhere (a save file, a database row, a network packet), and restored to resume the exact same sequence — forever, on any platform.
- **`IRandomSource`** unifies deterministic, shared (`Random.Shared`), and cryptographic randomness behind one interface, so range generation, shuffling, and picking are written once and work against any of them.
- **Weighted random sampling** (`PickWeighted`, `PickManyWeighted(Distinct)`, `WeightedSampler<T>`) ships with the library, with a precise exception contract and an `O(1)`-per-draw alias-method sampler for repeated weighted picks.
- **Zero dependencies.** No `PackageReference`, BCL only.

## Installation

```bash
dotnet add package SsalKit.Randomness
```

## Quick Start

```csharp
using SsalKit.Randomness;

// Seed a deterministic generator.
var rng = new DeterministicRandom(seed: 42);

int roll = rng.Next(1, 7);          // [1, 7)
double chance = rng.NextDouble();   // [0, 1)
bool coinFlip = rng.NextBoolean();

// Export the state (e.g. into a save file) and resume the exact same sequence later.
RandomState saved = rng.ExportState();
DeterministicRandom resumed = DeterministicRandom.FromState(saved);

// Derive an independent child stream (e.g. one per game entity) from a parent seed.
DeterministicRandom child = rng.Fork();

// Weighted pick, single shot.
string[] items = ["common", "rare", "legendary"];
long[] weights = [80, 18, 2];
string drop = rng.PickWeighted(items.AsSpan(), weights.AsSpan());

// Weighted pick, repeated: build once, draw O(1) per pick.
WeightedSampler<string> sampler = WeightedSampler<string>.Create(items, weights.AsSpan());
string anotherDrop = sampler.Pick(rng);
string[] tenDrops = sampler.PickMany(rng, count: 10);
```

## API Overview

| Type | Purpose |
|---|---|
| `IRandomSource` | Minimal contract (`NextUInt64()` + `NextBytes(Span<byte>)`) shared by every source. All higher-level operations are derived from these two members via extension methods. |
| `DeterministicRandom` | Seedable, state-exportable, forkable PRNG. `System.Random`-shaped instance API (`Next`, `NextInt64`, `NextDouble`, `NextSingle`, `NextBoolean`, `NextBytes`) plus `ExportState()`/`FromState(...)`/`Fork()`. |
| `RandomState` | `readonly record struct` holding the 256-bit state (`S0`..`S3`). Value-equatable, trivially JSON-serializable, with `ToArray()`/`FromSpan(...)`/`CopyTo(...)` for `ulong[4]` interop. |
| `CryptoRandomSource` | `IRandomSource` backed by `RandomNumberGenerator`. Unpredictable, thread-safe, exposed as `CryptoRandomSource.Instance`. |
| `SharedRandomSource` | `IRandomSource` backed by `Random.Shared`. Thread-safe, exposed as `SharedRandomSource.Instance`. |
| `SystemRandomSource` | `IRandomSource` adapter over any `Random` instance, for interop and tests. |
| `RandomSourceExtensions` | Uniform extensions on `IRandomSource`: `Next`/`NextInt64`/`NextDouble`/`NextSingle`/`NextBoolean`, `Shuffle`, `Pick`. Identical algorithm and output to `DeterministicRandom`'s instance methods. |
| `WeightedRandomExtensions` | `PickWeighted` (single shot, `long` or `double` weights, list or span form), `PickManyWeighted` (with replacement), `PickManyWeightedDistinct` (without replacement). |
| `WeightedSampler<T>` | Immutable, thread-safe, pre-built alias-method sampler for repeated weighted draws from a fixed `long`-weighted item set: `O(n)` build, `O(1)` per `Pick`/`PickMany`. |

## Algorithm & state contract (v1)

`DeterministicRandom`'s output sequence is **xoshiro256\*\***, and seed expansion (from a single `ulong` seed to the 256-bit internal state) is **SplitMix64**. The state is exactly four `ulong` words, exposed as `RandomState`.

This contract is permanently fixed for this type:

- **The same seed or the same restored state always produces the same sequence** — on any platform, in any process, forever.
- Because `RandomState` can be persisted as save data, changing the output sequence would corrupt every consumer's saved data. This will **never** happen in a patch or minor release.
- If the algorithm ever needs to evolve, it will ship as a **new type** (e.g. a hypothetical `DeterministicRandomV2`), never by changing the behavior of `DeterministicRandom` itself.
- The all-zero state is invalid (xoshiro256** can never leave it once entered) and is rejected by `FromState(...)`/`RandomState.FromSpan(...)` with an `ArgumentException`.

Derived guarantees:

- `Next(maxValue)` / `NextInt64(maxValue)` and their ranged overloads use **Lemire's multiply-shift-reject algorithm** — bias-free (no modulo bias), unlike `%`-based range reduction.
- `NextDouble()` returns `[0, 1)` with 53 bits of precision; `NextSingle()` returns `[0, 1)` with 24 bits. `1.0`/`1.0f` are never returned.
- `Fork()`'s contract is exactly `Fork() == new DeterministicRandom(this.NextUInt64())`: the parent draws one `ulong` (advancing its own sequence by exactly one step, same as any other `NextUInt64()` call) and expands it into the child's state via SplitMix64. Because the child's seed is 64 bits, the birthday-collision probability between independently forked children becomes meaningful only around `2^32` forks — far beyond the scale of any game or simulation workload.

## Thread safety

| Type | Thread-safe | Notes |
|---|---|---|
| `DeterministicRandom` | **No** | Concurrent access corrupts internal state and breaks sequence reproducibility. Use one instance per thread, or synchronize externally. |
| `CryptoRandomSource` | Yes | `RandomNumberGenerator.Fill` is static and thread-safe → exposed as a singleton. |
| `SharedRandomSource` | Yes | `Random.Shared` itself is thread-safe. |
| `SystemRandomSource` | Depends on the wrapped instance | A plain `new Random(seed)` is not thread-safe; `Random.Shared` is (prefer `SharedRandomSource` for that case). |
| `WeightedSampler<T>` | Yes (immutable) | The table is built once in `Create(...)`; every `Pick`/`PickMany` call only reads it and the caller-supplied `IRandomSource`. |

## Security

**`DeterministicRandom` is predictable.** Given a handful of consecutive outputs, its internal state can be reconstructed and every future output predicted. **Never** use it for tokens, credentials, shuffling anything that must stay secret, or any other security-sensitive purpose.

For those cases, use `CryptoRandomSource` instead. If you need `DeterministicRandom`'s reproducibility with an unpredictable seed, use `DeterministicRandom.CreateRandomlySeeded()`, which draws its seed from a cryptographic RNG — only the seed is unpredictable; the generator itself remains a predictable `DeterministicRandom` once created.

## Exceptions

The following contract applies uniformly across `RandomState`, the ranged-generation members, and every weighted-pick API (`WeightedRandomExtensions` and `WeightedSampler<T>`):

| Condition | Exception |
|---|---|
| `items` is empty | `ArgumentException` |
| A negative weight is present | `ArgumentException` (identifies the offending index) |
| A `double` weight is `NaN`/`Infinity` | `ArgumentException` (identifies the offending index) |
| The total weight is 0 | `ArgumentException` |
| A `long` weight sum overflows | `OverflowException` (checked summation) |
| `count <= 0` | `ArgumentOutOfRangeException` |
| In `PickManyWeightedDistinct`, `count` exceeds the number of items with strictly positive weight | `ArgumentOutOfRangeException` |
| `RandomState.FromState(...)` / `RandomState.FromSpan(...)` given the all-zero state | `ArgumentException` |
| `minValue > maxValue` in a ranged `Next`/`NextInt64` overload | `ArgumentOutOfRangeException` |

An item with weight `0` is **allowed** and simply never selected (only the total needs to be positive). In `PickManyWeightedDistinct`, the upper bound on `count` is the number of *positive*-weight items, not `items.Count` — a zero-weight item can never be drawn, so requiring more than that would mean either an infinite search or incorrectly returning a zero-weight item.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
