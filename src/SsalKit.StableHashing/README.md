[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ja.md)

# SsalKit.StableHashing

Platform- and process-independent 64-bit checksums via a version-locked canonical encoding contract: `[StableHashContract]`/`[StableHashMember]` drive a source generator that writes `ComputeStableHash()` for you, hashed with an internally ported XxHash64. Zero dependencies.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.StableHashing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.StableHashing)

## Why SsalKit.StableHashing?

`object.GetHashCode()` looks like it does this job, but the BCL is explicit that it does not: its documented contract permits the value to differ between runs of the same program, between processes, and across .NET versions, and warns against ever persisting it or relying on it staying the same anywhere outside the lifetime of one object. That rules out exactly the use cases where you actually want a "hash of this object" — a checksum saved to a database, sent over a network, compared between two machines, or compared between a run today and a run next month.

`System.IO.Hashing` (the BCL's own `XxHash64`, `XxHash3`, etc.) does not fill that gap either — on purpose. It hashes bytes you already have; it has no opinion about how to turn a C# object into those bytes in a way that stays consistent forever. That "how to turn an object into bytes" decision — field order, numeric width, string encoding, what happens to `-0.0` or `1.0m` vs `1.00m` — is exactly the part that has to be nailed down and never changed again, or every previously computed hash silently goes stale. That's the actual product here: **a canonical encoding contract**, not a hash algorithm. The hash algorithm is comparatively replaceable; the encoding rules are not.

SsalKit.StableHashing:

- **`[StableHashContract]` / `[StableHashMember(id)]`** mark a type and its members. A source generator bundled in the package writes a `ComputeStableHash()` extension method for you at compile time — no reflection, AOT- and trimming-safe.
- **The encoding is a permanently fixed v1 contract** (byte order, field widths, string encoding, nested-contract recursion — see below), so a hash computed today and a hash computed by a future patch of this library, on a different machine or architecture, are the same value for the same logical input, forever.
- **Equality-consistent by construction.** `decimal`, `DateTimeOffset`, and `float`/`double` each have a trap where `==` is true but the underlying bits differ; the encoding normalizes all three so that `a == b` always implies `encode(a) == encode(b)` (see Equality-consistency invariant, below).
- **Zero dependencies.** The hash algorithm (XxHash64) is ported internally rather than pulled in from `System.IO.Hashing`, so the package stays BCL-only.

Typical uses: detecting when two lockstep/replay simulations have desynced (compare a per-tick hash instead of the whole state), skipping a redundant snapshot save when nothing actually changed, deterministic A/B bucketing (`hash % 100`), and deriving a reproducible, named random stream: `StableHash64.Value` can be handed straight to `new SsalKit.Randomness.DeterministicRandom(hash.Value)` (from the separate, optional [SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md) package) to turn any hashable value into a seed. The two packages have no dependency on each other — this is a documented usage pattern, not a coupling.

## Installation

```bash
dotnet add package SsalKit.StableHashing
```

The package contains both the runtime types (`StableHash64`, `StableHashWriter`, the two attributes) and the source generator — no separate analyzer package to install, and no `PackageReference` of its own.

## Quick Start

```csharp
using SsalKit.StableHashing;

[StableHashContract("game.player-snapshot", Version = 1)]
public sealed record PlayerSnapshot
{
    [StableHashMember(1)] public string PlayerId { get; init; } = "";

    [StableHashMember(2)] public int Level { get; init; }

    [StableHashMember(3)] public long Gold { get; init; }
}

var snapshotA = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };
var snapshotB = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };

StableHash64 hashA = snapshotA.ComputeStableHash();
StableHash64 hashB = snapshotB.ComputeStableHash();

hashA == hashB;      // true -- two separate instances, same member values
hashA.ToString();     // "9c3f38517dbc66aa" -- lowercase, 16-char hex
hashA.Value;          // the raw ulong
```

`[StableHashMember]` is opt-in: a member without it is simply excluded from the contract, no diagnostic. Members are encoded in ascending `Id` order, not declaration order, so reordering or renaming members in source never changes the hash — only changing a member's `Id`, or the type of value it holds, does.

## API Overview

| Type | Purpose |
|---|---|
| `StableHashContractAttribute(string name)` | Marks a `class`/`struct` as a contract. `Name` is the contract's permanent identity (independent of the CLR type name, so the type can be freely renamed); `Version` (default `1`) is bumped when the member set or member types change in a way that should invalidate previously stored hashes. |
| `StableHashMemberAttribute(int id)` | Marks a field or property as part of the contract, with the stable id (`>= 1`) encoded ahead of its value. Members without this attribute are excluded. |
| `StableHash64` | `readonly record struct` wrapping the `ulong` result. `ToString()` renders lowercase, zero-padded 16-character hex. Hand `.Value` to `DeterministicRandom` for a named seed (see above). |
| `StableHashWriter` | The low-level, allocation-free `ref struct` the generator emits calls against — usable directly for types the generator doesn't (yet) cover. See Encoding contract, below, for its exact rules. |
| generated `ComputeStableHash()` / `AppendStableHash(ref StableHashWriter)` | One `public static class {Type}StableHashing` per contract type, in the contract's own namespace, with these two extension methods. `AppendStableHash` is what nested-contract members call into; a `class` contract's `ComputeStableHash()` throws `ArgumentNullException` on a null receiver. |

## Encoding contract (v1)

**This encoding is a permanent, versioned contract.** Every rule below — byte order, field widths, the floating-point/decimal normalization rules, the leading format marker — is fixed forever. Changing any of it would silently change every hash this library has ever produced, corrupting every consumer's stored checksums. If the encoding ever needs to evolve, it ships as a new, separate API (e.g. a hypothetical `StableHash128`/`StableHashWriterV2`), never by changing this one's behavior.

Every produced stream starts with a single format-marker byte (`0x01`), then the contract header: the contract name (as a length-prefixed string, below) followed by its `Version` as a little-endian `int32`. A member's value is preceded by its member id (little-endian `int32`). All fixed-width integers are little-endian.

| Type | Encoding |
|---|---|
| `bool` | 1 byte (`0x00`/`0x01`) |
| `sbyte`…`ulong`, `Int128`/`UInt128` | fixed-width, little-endian |
| `char` | its UTF-16 code unit, little-endian `ushort` |
| `enum` | its underlying type's encoding (renaming a member is safe; changing a member's underlying value changes the hash) |
| `float` / `double` | normalized per the equality-consistency invariant below, then the bit pattern, little-endian |
| `decimal` | normalized per the equality-consistency invariant below, then sign (1 byte) + scale (1 byte) + 96-bit mantissa (12 bytes, little-endian) |
| `string` | little-endian `int32` UTF-8 byte count, then the UTF-8 bytes (malformed UTF-16 falls back to `Encoding.UTF8`'s deterministic replacement-character behavior) |
| `Guid` | RFC 4122 big-endian 16 bytes — `Guid.TryWriteBytes(span, bigEndian: true, out _)`, matching the string representation's byte order |
| `DateOnly` | `DayNumber`, little-endian `int32` |
| `TimeOnly` / `TimeSpan` | `Ticks`, little-endian `int64` |
| `DateTimeOffset` | **only** `UtcTicks`, little-endian `int64` — see equality-consistency invariant below |
| `T?` (`Nullable<T>` / nullable reference) | 1-byte marker (`0x00` absent / `0x01` present followed by the value); a non-nullable member carries no marker at all |
| `T[]`, `List<T>`, `IReadOnlyList<T>`, `ImmutableArray<T>` | little-endian `int32` element count, then each element recursively encoded in order (the element type must itself be a supported type; nesting is allowed) |
| another `[StableHashContract]` type | that contract's full encoding, recursively, header included — a nested contract's own version/name change propagates correctly into every hash that holds it |

**v1 rejects the following, as a compile-time diagnostic rather than a runtime surprise:** `DateTime` (`SSALH003` — use `DateTimeOffset` or `DateOnly`), `Dictionary`/`HashSet`/any other unordered or arbitrary `IEnumerable<T>` (enumeration order isn't guaranteed), `object`, delegates, pointers, interfaces and abstract types (the runtime type isn't known at compile time), a user-defined type with no `[StableHashContract]`, a circular contract graph, and a generic contract type.

## Equality-consistency invariant

> For every supported type, **`a == b` implies `encode(a) == encode(b)`.**

Three BCL types have a trap where this would otherwise silently break — two values that compare equal, but whose underlying bits differ — so the encoding normalizes each one before writing it:

| Type | Trap | v1 rule |
|---|---|---|
| `decimal` | `1.0m == 1.00m`, but the underlying scale (and therefore the bits) differ | Normalize by dividing the 96-bit mantissa by 10 while the scale is positive and the mantissa divides evenly (integer arithmetic only, at most 28 iterations) — `1.0m` and `1.00m` encode identically. Every zero representation (`0m`, `-0.0m`, `0.00m`, …) normalizes to one canonical zero encoding (sign `0x00`, scale `0`, mantissa `0`), since decimal equality doesn't distinguish the sign of zero either. |
| `DateTimeOffset` | The same instant at a different offset compares equal (`1pm+01:00 == noon+00:00`) | Encode **only** `UtcTicks`; the offset is deliberately excluded. If the offset itself is meaningful, store it as a separate member. |
| `float` / `double` | `-0.0 == +0.0` but the bit patterns differ; NaN payload bits are not portable across platforms (x86 vs. ARM) | Normalize negative zero to positive zero, and every NaN bit pattern to a single canonical quiet NaN (`0x7FC00000` for `float`, `0x7FF8000000000000` for `double`), before writing the bit pattern. |

Two more totality rules fall out of the same principle:

- **`string`**: malformed UTF-16 (an unpaired surrogate) uses `Encoding.UTF8`'s default replacement-character (`U+FFFD`) fallback, which is itself deterministic — there is no input that fails to encode.
- **`ImmutableArray<T>`**: `default(ImmutableArray<T>)` (uninitialized) is treated as empty, matching how most code already treats the two interchangeably, and keeping the writer a total function over every value of the type.

The invariant only runs one direction: `encode(a) == encode(b)` does **not** imply `a == b` — see Hash semantics, below.

## Diagnostics

| ID | Severity | Reported when |
|---|---|---|
| `SSALH001` | Error | Two or more members of the same contract declare the same `[StableHashMember]` id. |
| `SSALH002` | Error | A member's type is not one of the v1 supported types (see the rejected list above). |
| `SSALH003` | Error | A member is `System.DateTime` — use `DateTimeOffset` (for an instant) or `DateOnly` (for a calendar date) instead. |
| `SSALH004` | Error | A member's type is a user-defined type with no `[StableHashContract]`. |
| `SSALH005` | Error | Following `[StableHashContract]` member types from this type eventually cycles back to it. |
| `SSALH006` | Error | A `class` contract is not `sealed` (also reported for a `static class`, which can't be an extension parameter's type at all). |
| `SSALH007` | Error | A member can't be read from the generated extension class (`private`/`protected`, `static`, an indexer, or write-only) — or the contract type itself, or a type it's nested inside, isn't accessible to generated code. |
| `SSALH008` | Error | A `[StableHashMember(id)]` id is less than 1. |
| `SSALH009` | Error | A `[StableHashContract]` name is null/whitespace, or `Version` is less than 1. |
| `SSALH010` | Warning | A contract declares zero `[StableHashMember]` members — every instance hashes to the same value (the header alone). |
| `SSALH011` | Warning | Two or more contract types in the compilation declare the same `[StableHashContract]` name. |
| `SSALH012` | Warning | `[StableHashMember]` is applied to a member whose declaring type has no `[StableHashContract]` — an orphan attribute, nothing is generated for it. |
| `SSALH013` | Error | `[StableHashContract]` is applied to a generic type, or one nested inside a generic type (v1 doesn't support open contract types). |

Every error above suppresses generation for that contract type entirely — there is no partial generation. A warning never blocks generation.

## Hash semantics

`StableHash64` is a 64-bit fingerprint, and every fixed-width hash gives you the same asymmetric guarantee, not a symmetric one:

- **If two hashes differ, the underlying values are certainly different (100%).** This falls straight out of `encode`/hash being deterministic functions: equal inputs cannot possibly land on different outputs.
- **If two hashes are equal, the underlying values are *almost certainly* the same — but not guaranteed.** With a 64-bit output space, the birthday bound puts the point where collisions become likely at around 2^32 (~4.3 billion) distinct hashed values. For comparing a handful of values — two simulation states, a snapshot against its predecessor, a cache key — the odds of an accidental collision are astronomically small. They are not zero.

**`StableHash64` is therefore not suitable as the final word on identity where a collision would be catastrophic** — deduplication that silently discards data on a false match, for instance. It's the right tool for cheap, fast comparison (desync detection, change detection, cache/ETag material, deterministic bucketing) where an essentially-never event causing a false positive is an acceptable, or independently-checked, risk.

## Security

**`StableHash64` is not a cryptographic hash.** There is no collision-resistance guarantee against an adversary who deliberately constructs two different inputs that hash identically, no keying, and no protection against tampering. Do not use it for integrity checks against a malicious modifier, message authentication, password storage, digital signatures, or any other security-sensitive purpose — use `System.Security.Cryptography.SHA256` or similar for those.

## Performance

SsalKit.StableHashing is built around one performance contract: **zero bytes allocated per `ComputeStableHash()` call.** `StableHashWriter` is a `ref struct` that streams every value straight into the hasher's state (batched through a small inline staging buffer) — there is no intermediate `byte[]` serialization buffer, on any code path, including the string-encoding fallback for inputs too large for its stack buffer (which rents from `ArrayPool<byte>` instead of allocating).

Measured with BenchmarkDotNet v0.15.8, .NET 10.0.10, AMD Ryzen 9 3950X, Windows 11 (SsalKit.StableHashing 0.0.4). Numbers vary by hardware; reproduce them with the [benchmark project](https://github.com/ssalkit/ssalkit/tree/main/benchmarks/SsalKit.StableHashing.Benchmarks). Allocated is 0 B in every row except the naive baseline.

| Scenario | Time | Allocated |
|---|---:|---:|
| Small contract (4 scalar members) | 112.9 ns | 0 B |
| Medium contract (string + nested contract, 12 members) | 321.2 ns | 0 B |
| Collection member, 10 / 100 / 1000 elements | 142 ns / 787 ns / 7.38 μs | 0 B |
| String member, ASCII / Korean / long (pool fallback) | 100 ns / 134 ns / 251 ns | 0 B |
| Naive baseline: manual serialize-then-hash | 242 ns | 632 B |
| Generated `ComputeStableHash()`, same payload as the baseline above | 370 ns | 0 B |

Read the last two rows together, honestly: the naive approach — manually serializing a value into a `byte[]` and hashing that buffer — comes out *faster* in raw time (242 ns vs. 370 ns) than the generated streaming code. What it costs is 632 bytes allocated on every single call, where the generated path allocates nothing. That's a deliberate trade, not an oversight: on a tick loop, a save path, or any other hot path invoked thousands of times a second, zero GC pressure per call is worth more than a ~130 ns difference — the naive version's 632 B/call adds up to real collection pauses at scale, while the streaming design's cost stays flat regardless of call frequency. If `ComputeStableHash()` runs rarely (once per HTTP request, say), the raw-speed edge of hand-rolled serialization may matter less than never having to think about its allocation cost in the first place — which is what the generated path buys you either way.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
