# SsalKit.StableHashing — AI contract sheet

Platform- and process-independent 64-bit checksums via a version-locked canonical encoding contract. `[StableHashContract]`/`[StableHashMember(id)]` drive a bundled source generator that writes `ComputeStableHash()` for a type at compile time, hashed with an internally ported `XxHash64`.

- **TFM:** `net10.0`. **Package dependencies:** none (BCL only). Hash algorithm is ported internally, not pulled from `System.IO.Hashing`.
- **Bundled analyzer:** `SsalKit.StableHashing.Generator` (`netstandard2.0`) ships inside the package under `analyzers/dotnet/cs`. No separate package.
- **Namespace:** `SsalKit.StableHashing` (all public types).
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 1. API surface

### Pick the right construct

| Requirement | Use |
|---|---|
| Give a type a portable, persistable 64-bit checksum | `[StableHashContract("name")]` on the type + `[StableHashMember(id)]` on each member to include |
| Compute the hash | `value.ComputeStableHash()` (generated extension method) |
| Encode a nested contract member from hand-written code | the generated `value.AppendStableHash(ref writer)` |
| Hand-write encoding for a type the generator doesn't cover | `StableHashWriter` directly (low-level, allocation-free `ref struct`) |
| Derive a reproducible named random seed from a hash | `new SsalKit.Randomness.DeterministicRandom(hash.Value)` — separate, optional package, no dependency either direction |
| A **cryptographic** hash (integrity, signing, passwords) | **not this package** — use `System.Security.Cryptography.SHA256` or similar |

### `StableHashContractAttribute(string name)` — `[AttributeUsage(Class | Struct)]`

| Member | Contract |
|---|---|
| `string Name { get; }` | The contract's permanent identity, encoded into the header of every hash for this type. Independent of the CLR type name — renaming the type never changes the hash; changing `Name` always does. |
| `int Version { get; set; }` | Default `1`. Bump when the member set or a member's type changes in a way that should invalidate previously stored hashes. Must be `>= 1` (`SSALH009` otherwise). |

### `StableHashMemberAttribute(int id)` — `[AttributeUsage(Property | Field)]`

| Member | Contract |
|---|---|
| `int Id { get; }` | The member's stable id (`>= 1`, `SSALH008` otherwise), encoded immediately before the member's value. Members are encoded in ascending `Id` order, **not declaration order** — reordering/renaming a member in source never changes the hash; changing its `Id` or its value's type does. A member without this attribute is simply excluded — no diagnostic. |

### `StableHash64` — `readonly record struct (ulong Value)`

| Member | Contract |
|---|---|
| `ulong Value` | The raw 64-bit hash. |
| `string ToString()` | Lowercase, zero-padded 16-character hex (e.g. `"9c3f38517dbc66aa"`). Part of the permanent contract. |
| Equality | Value equality via the `record struct`-generated members (`==`, `Equals`, `GetHashCode`). |

### `StableHashWriter` — `public ref struct`, generator's low-level target

Stack-only (`ref struct`), not thread-safe (inherently single-threaded — cannot escape the stack), allocation-free. `Create()` writes the leading `0x01` format marker immediately.

| Member | Contract |
|---|---|
| `static StableHashWriter Create()` | New writer; writes the `0x01` format marker. |
| `void AppendContractHeader(string contractName, int version)` | `AppendString(contractName)` then `AppendInt32(version)`. Called once per contract, including once per nested contract value. |
| `void AppendMemberId(int memberId)` | `AppendInt32(memberId)`. Generated code calls this immediately before each member's value. |
| `void AppendNullMarker(bool hasValue)` | 1 byte: `0x01` if `hasValue` (value follows), `0x00` otherwise. Only for nullable members — non-nullable members never call this. |
| `void AppendCount(int count)` | `AppendInt32(count)`. Written before a collection's elements. |
| `void AppendBoolean/AppendChar/AppendSByte/AppendByte/AppendInt16/AppendUInt16/AppendInt32/AppendUInt32/AppendInt64/AppendUInt64/AppendInt128/AppendUInt128(...)` | Fixed-width, little-endian, per the Encoding contract table in §2. |
| `void AppendSingle(float)` / `void AppendDouble(double)` | Normalizes `-0` → `+0` and any NaN → the canonical quiet NaN bit pattern, **then** writes the bit pattern little-endian. |
| `void AppendDecimal(decimal)` | Normalizes trailing-zero mantissa digits away (integer arithmetic, ≤28 iterations), forces a single canonical zero encoding, then writes sign (1B) + scale (1B) + 96-bit mantissa (12B LE). |
| `void AppendString(string)` | `ArgumentNullException` on null. `int32` UTF-8 byte count (LE) + UTF-8 bytes. Allocation-free up to 256 UTF-8 bytes; rents from `ArrayPool<byte>.Shared` beyond that (never allocates). |
| `void AppendGuid(Guid)` | RFC 4122 big-endian 16 bytes (`Guid.TryWriteBytes(span, bigEndian: true, out _)`). |
| `void AppendDateOnly(DateOnly)` | `DayNumber`, `int32` LE. |
| `void AppendTimeOnly(TimeOnly)` / `void AppendTimeSpan(TimeSpan)` | `Ticks`, `int64` LE. |
| `void AppendDateTimeOffset(DateTimeOffset)` | **Only** `UtcTicks`, `int64` LE — the offset is deliberately not encoded. |
| `StableHash64 Finish()` | Flushes and digests. The writer must not be used again afterward. |

There is no `AppendEnum`: generated code casts an enum member to its underlying type and calls the matching `Append*` overload.

### Generated per `[StableHashContract]` type: `public static class {FlattenedTypeName}StableHashing`

Emitted as a top-level class into the contract type's own namespace (flattened name for a nested type, e.g. `Outer_InnerStableHashing`), so its extension methods are already in scope wherever the contract type is used. The contract type itself does **not** need to be `partial`.

| Member | Contract |
|---|---|
| `static StableHash64 ComputeStableHash(this T value)` | Creates a writer, calls `AppendStableHash`, returns `Finish()`. For a `class` contract: `ArgumentNullException` when `value` is null. |
| `static void AppendStableHash(this T value, ref StableHashWriter writer)` | Writes the contract header then every `[StableHashMember]`, ascending `Id` order. What a *nested* contract member's encoding calls into. |

## 2. Contracts (versioned / immutable)

**Encoding contract (v1), permanently fixed.** Format marker `0x01` first, then header (`AppendString(contractName)` + `AppendInt32(version)`), then each member as `AppendMemberId(id)` + its value. All fixed-width integers little-endian.

| Type | Encoding |
|---|---|
| `bool` | 1 byte (`0x00`/`0x01`) |
| `sbyte`…`ulong`, `Int128`/`UInt128` | fixed-width LE |
| `char` | UTF-16 code unit, `ushort` LE |
| `enum` | underlying type's encoding |
| `float`/`double` | normalize `-0`→`+0`, NaN→canonical quiet NaN, then bit pattern LE |
| `decimal` | normalize trailing-zero mantissa digits + canonical zero, then sign(1B)+scale(1B)+mantissa(12B LE) |
| `string` | `int32` UTF-8 byte count LE + UTF-8 bytes (malformed UTF-16 → `U+FFFD` fallback) |
| `Guid` | RFC 4122 big-endian 16 bytes |
| `DateOnly` | `DayNumber` `int32` LE |
| `TimeOnly`/`TimeSpan` | `Ticks` `int64` LE |
| `DateTimeOffset` | **only** `UtcTicks` `int64` LE (offset excluded) |
| `T?` (`Nullable<T>`/nullable ref) | 1-byte marker (`0x00` null / `0x01` + value); non-nullable members carry no marker |
| `T[]`, `List<T>`, `IReadOnlyList<T>`, `ImmutableArray<T>` | `int32` count LE + elements recursively encoded in order |
| another `[StableHashContract]` type | that contract's full encoding recursively, header included |

**v1 rejects (compile-time diagnostic, not runtime failure):** `DateTime` (`SSALH003`), `Dictionary`/`HashSet`/unordered or arbitrary `IEnumerable<T>` (`SSALH002`), `object`/delegate/pointer/interface/abstract type (`SSALH002`), a user type with no `[StableHashContract]` (`SSALH004`), a circular contract graph (`SSALH005`), a generic contract type (`SSALH013`).

**Equality-consistency invariant:** for every supported type, `a == b` implies `encode(a) == encode(b)` (one direction only — `encode(a) == encode(b)` does **not** imply `a == b`, this is a hash, not an identity function).

| Type | Trap | v1 rule |
|---|---|---|
| `decimal` | `1.0m == 1.00m`, different scale/bits | Strip trailing-zero mantissa digits (integer division by 10 while `scale > 0` and divisible, ≤28 iterations); every zero (`0m`, `-0.0m`, `0.00m`, …) → one canonical zero encoding. |
| `DateTimeOffset` | Same instant, different offset, `==` true | Encode **only** `UtcTicks`; offset excluded on purpose. Store the offset as a separate member if it matters. |
| `float`/`double` | `-0.0 == +0.0`, different bits; NaN payload not portable across platforms | Normalize `-0`→`+0`; every NaN → single canonical quiet NaN (`0x7FC00000` / `0x7FF8000000000000`) before writing the bit pattern. |
| `string` | malformed UTF-16 (unpaired surrogate) | `Encoding.UTF8` deterministic replacement-character (`U+FFFD`) fallback. |
| `ImmutableArray<T>` | `default` vs `Empty` | `default(ImmutableArray<T>)` treated as empty. |

**Algorithm contract (v1), permanently fixed:** hash algorithm is `XxHash64` (internal port, seed `0`), applied to the byte stream produced above. **The algorithm itself is part of the versioned contract** — it will never change for `StableHash64`; evolution (e.g. a 128-bit hash) ships as a new type, never as a behavior change to this one.

**Hash semantics — asymmetric guarantee, not a symmetric one:**
- Different hashes ⇒ certainly different inputs (100%, deterministic function).
- Same hash ⇒ *almost certainly* same input, **not guaranteed** — 64-bit output space, birthday-bound collisions become likely around 2^32 (~4.3B) distinct hashed values. Not safe as the sole/final identity check where a collision would be catastrophic (e.g. silent-data-loss deduplication).

**Security:** `StableHash64` is **not cryptographic**. No collision resistance against a deliberate adversary, no keying, no tamper protection. Never for integrity checks, message authentication, password storage, or signatures.

**Performance contract:** zero bytes allocated per `ComputeStableHash()` call, on every code path including the string-encoding fallback for inputs beyond the writer's stack buffer (rents from `ArrayPool<byte>.Shared` instead of allocating). See README's Performance section for the naive-serialize-then-hash comparison (faster in raw time, but 632 B/call vs. 0 B/call).

### Thread safety

| Type | Thread-safe |
|---|---|
| `StableHashWriter` | Inherently single-threaded (`ref struct`, stack-only, cannot be shared). |
| generated `ComputeStableHash()` | Yes — stateless pure function. |

## 3. DO NOT

- **DO NOT use `StableHash64` for anything security-sensitive.** No collision resistance, no keying, no tamper protection. Use `System.Security.Cryptography.SHA256` (or similar) for integrity/signing/password/authentication purposes.
- **DO NOT treat a hash match as a final identity determination where a false positive is catastrophic.** Same hash means *almost certainly* the same input, not provably the same input — see the birthday-bound note in §2.
- **DO NOT put `[StableHashMember]` on a `System.DateTime` member.** It's rejected at compile time (`SSALH003`) because `Kind` (Utc/Local/Unspecified) makes the value ambiguous for a portable encoding. Use `DateTimeOffset` (instant) or `DateOnly` (calendar date).
- **DO NOT expect `DateTimeOffset`'s offset to be encoded.** Only `UtcTicks` is encoded — two values representing the same instant at different offsets hash identically by design (matches `DateTimeOffset` equality). Store the offset as its own `[StableHashMember]` if it's meaningful to the contract.
- **DO NOT put `[StableHashMember]` on a `Dictionary`, `HashSet`, or any other type whose enumeration order isn't guaranteed.** Rejected at compile time (`SSALH002`) — an unordered collection would make the hash depend on iteration order, which isn't stable.
- **DO NOT expect a non-`sealed` `class` contract to work.** `SSALH006` — a derived instance encoded through the base contract would silently drop the derived state. `struct`/`record struct` contracts don't need `sealed` (no derived-instance case). A `static class` is rejected too.
- **DO NOT expect member declaration order, or the CLR member name, to affect the hash.** Only `[StableHashMember(id)]`'s `Id` (ascending) determines encoding order; renaming a member or the declaring type is always safe. Changing the `Id`, the `[StableHashContract]` `Name`/`Version`, or a member's value-type is never safe (all three change the hash on purpose).
- **DO NOT assume a member is included by default.** `[StableHashMember]` is strictly opt-in; a member without it is silently excluded, with no diagnostic. A contract with zero `[StableHashMember]` members compiles and hashes every instance identically (`SSALH010`, warning).
- **DO NOT nest a member whose type has no `[StableHashContract]`.** Rejected at compile time (`SSALH004`). Add the attribute to the nested type, or drop `[StableHashMember]` from the member.
- **DO NOT create a cycle in the contract graph** (type A holds a `[StableHashMember]` of type B, which (transitively) holds one of type A). Rejected at compile time (`SSALH005`) — encoding recurses through nested contracts and would recurse forever.
- **DO NOT expect a generic type, or a type nested inside a generic type, to take `[StableHashContract]`.** Rejected (`SSALH013`) — the generated extension methods are non-generic with a receiver of the contract's own closed type.
- **DO NOT call `StableHashWriter`'s `Append*` methods out of order, or inconsistently, across two encodings of "the same" logical value if hand-writing encoding logic.** Unlike generated code, a hand-written caller is fully responsible for consistency — getting it wrong does not throw, it just silently produces a hash that stops being stable for that value.
- **DO NOT reuse a `StableHashWriter` after calling `Finish()`.**
- **DO NOT expect `System.IO.Hashing` to be a dependency.** `XxHash64` is ported internally; the package has zero `PackageReference`s.

## 4. Diagnostics

Prefix `SSALH`, category `SsalKit.StableHashing`. When any **error** fires for a contract type, **no extension class is generated for it at all** — no partial generation. A **warning** never blocks generation.

| ID | Trigger | Fix |
|---|---|---|
| `SSALH001` | Two or more members of the same contract declare the same `[StableHashMember]` id. | Give each member a unique id. |
| `SSALH002` | A member's type is not a v1-supported type (`Dictionary`/`HashSet`/unordered `IEnumerable<T>`, `object`, delegate, pointer, interface, abstract type, etc.). | Use a supported type, or remove `[StableHashMember]` from it. |
| `SSALH003` | A member is `System.DateTime`. | Use `DateTimeOffset` (instant) or `DateOnly` (calendar date). |
| `SSALH004` | A member's type is user-defined with no `[StableHashContract]`. | Add `[StableHashContract]` to that type, or drop `[StableHashMember]` from the member. |
| `SSALH005` | The contract graph reachable from this type's members cycles back to this type. | Break the cycle — remove `[StableHashMember]` from one member on the cycle. |
| `SSALH006` | A `class` contract is not `sealed` (including a `static class`). | Seal the class, or convert to `struct`/`record struct`. |
| `SSALH007` | A member (or the contract type itself, or a containing type) isn't accessible to the generated top-level extension class — `private`/`protected`/`private protected`, `static`, an indexer, write-only, or `file`-local. | Make the whole chain readable and at least `internal`, not `file`-local; make the member a readable instance property/field. |
| `SSALH008` | A `[StableHashMember(id)]` id is `< 1`. | Use `id >= 1`. |
| `SSALH009` | `[StableHashContract]` name is null/whitespace, or `Version < 1`. | Give the contract a non-blank name and `Version >= 1`. |
| `SSALH010` | A contract declares zero `[StableHashMember]` members. | Add members if any were meant to be included; otherwise this is informational only. |
| `SSALH011` | Two or more contract types in the compilation share a `[StableHashContract]` name. | Rename one, unless the alias is intentional (e.g. a migration). |
| `SSALH012` | `[StableHashMember]` is on a member of a type with no `[StableHashContract]`. | Add `[StableHashContract]` to the declaring type, or remove the orphan attribute. |
| `SSALH013` | `[StableHashContract]` is on a generic type, or one nested inside a generic type. | Use a closed (non-generic) type, or hand-write encoding against `StableHashWriter`. |

## 5. Canonical snippets

### Define a contract and hash it

```csharp
using SsalKit.StableHashing;

[StableHashContract("game.player-snapshot", Version = 1)]
public sealed record PlayerSnapshot
{
    [StableHashMember(1)] public string PlayerId { get; init; } = "";

    [StableHashMember(2)] public int Level { get; init; }

    [StableHashMember(3)] public long Gold { get; init; }
}

var snapshot = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };
StableHash64 hash = snapshot.ComputeStableHash();

string hex = hash.ToString();   // "9c3f38517dbc66aa" -- lowercase, 16 hex chars
ulong raw = hash.Value;
```

### Nested contract member

```csharp
using SsalKit.StableHashing;

[StableHashContract("game.position", Version = 1)]
public sealed record Position
{
    [StableHashMember(1)] public int X { get; init; }

    [StableHashMember(2)] public int Y { get; init; }
}

[StableHashContract("game.entity", Version = 1)]
public sealed record Entity
{
    [StableHashMember(1)] public string Id { get; init; } = "";

    [StableHashMember(2)] public Position Position { get; init; } = new();   // recurses into Position's own encoding
}
```

### Equality-consistency in practice

```csharp
var a = new Probe { Amount = 1.0m,  Timestamp = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero) };
var b = new Probe { Amount = 1.00m, Timestamp = new DateTimeOffset(2026, 7, 30, 21, 0, 0, TimeSpan.FromHours(9)) }; // same instant

a.ComputeStableHash() == b.ComputeStableHash();   // true -- decimal scale and DateTimeOffset offset are both normalized away

[StableHashContract("probe", Version = 1)]
public sealed record Probe
{
    [StableHashMember(1)] public decimal Amount { get; init; }
    [StableHashMember(2)] public DateTimeOffset Timestamp { get; init; }
}
```

### Deriving a reproducible random seed (optional pairing with SsalKit.Randomness)

```csharp
using SsalKit.Randomness;
using SsalKit.StableHashing;

var seedKey = new ShopSeedKey { PlayerId = "player-42", DayNumber = 19 };
StableHash64 seedHash = seedKey.ComputeStableHash();

var rng = new DeterministicRandom(seedHash.Value);   // same key -> same seed -> same sequence, always

[StableHashContract("shop-seed-key", Version = 1)]
public readonly record struct ShopSeedKey
{
    [StableHashMember(1)] public string PlayerId { get; init; }
    [StableHashMember(2)] public int DayNumber { get; init; }
}
```

### Hand-written encoding against `StableHashWriter` (for a type the generator doesn't cover)

```csharp
using SsalKit.StableHashing;

public static class ManualEncoding
{
    public static StableHash64 Hash(string id, int amount)
    {
        var writer = StableHashWriter.Create();
        writer.AppendContractHeader("manual.example", version: 1);

        writer.AppendMemberId(1);
        writer.AppendString(id);

        writer.AppendMemberId(2);
        writer.AppendInt32(amount);

        return writer.Finish();
    }
}
```
