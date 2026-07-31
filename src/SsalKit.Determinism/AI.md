# SsalKit.Determinism — AI contract sheet

Opt-in compile-time diagnostics for non-deterministic APIs. `[Deterministic]` marks a type or member whose code has to be reproducible; a bundled analyzer reports `SSALD` warnings for banned APIs **called directly** inside that scope, and every message names a concrete replacement.

- **TFM:** `net10.0`. **Package dependencies:** none. The runtime assembly contains two attributes and no logic.
- **Bundled analyzer:** `SsalKit.Determinism.Analyzer` (`netstandard2.0`) ships inside the package under `analyzers/dotnet/cs`. No separate package, no source generator — this package generates nothing.
- **Namespace:** `SsalKit.Determinism`.
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 0. Read this first — what the analysis is not

**The analysis is shallow: it only sees direct calls, and no diagnostics is not a proof of determinism.** It is an assistive tool, not a guarantee.

| Not detected | Why |
|---|---|
| Indirect calls | A `[Deterministic]` method calling an unmarked helper that reads `DateTime.Now` reports nothing. Mark the helper types too. |
| `Dictionary`/`HashSet` enumeration order | Deliberately out of scope for v1 — order-independent consumption cannot be told apart from order-dependent. |
| Floating-point cross-platform differences (FMA, x87, vectorization) | Outside static analysis entirely. |
| Culture-dependent formatting/parsing (`ToString()`, `Parse`) | Already covered by CA1304/CA1305/CA1310 — enable those instead of expecting it here. |
| Reflection-dispatched calls | The symbol is not known at compile time. |
| `await` resumption context, thread affinity | Only the listed scheduling entry points are caught, not the consequences of awaiting. |

Every rule is a **Warning** and none will ever default to Error. Raise severity per id in `.editorconfig` if a build gate is wanted.

## 1. API surface

The whole runtime package is two attributes.

### Pick the right construct

| Requirement | Use |
|---|---|
| Make a simulation core / replay path / cache-key computation analyzed | `[Deterministic]` on the type |
| Analyze one method, constructor, or property only | `[Deterministic]` on that member |
| Exempt one member or nested type from an enclosing scope | `[AllowNonDeterminism(Justification = "...")]` |
| Suppress one call site | `#pragma warning disable SSALD00x` |
| Turn a whole category off / into an error | `.editorconfig`: `dotnet_diagnostic.SSALD006.severity = none` |
| Ban APIs across a whole project, not a scope | **not this package** — `Microsoft.CodeAnalysis.BannedApiAnalyzers` |

### `DeterministicAttribute` — `[AttributeUsage(Class | Struct | Method | Constructor | Property, Inherited = false)]`

No properties. Applied to a type, the scope covers every member of that type **and every nested type**, lexically.

### `AllowNonDeterminismAttribute` — `[AttributeUsage(Class | Struct | Method | Constructor | Property, Inherited = false)]`

| Member | Contract |
|---|---|
| `string? Justification { get; set; }` | Documentary only. Nothing reads it at run time; no diagnostic requires it. Its absence changes no behaviour. |

## 2. Contracts (versioned / immutable)

**Scope resolution — lexical, nearest-wins.** From the code under analysis, walk outward through containing members and containing types:

1. `[Deterministic]` found first → in scope, analyzed.
2. `[AllowNonDeterminism]` found first → exempt, silent.
3. Neither found → outside every scope, silent.

| Case | Behaviour |
|---|---|
| Lambda, local function, field/property initializer | Naturally in the enclosing member's or type's scope; no special rule. |
| Nested type inside a marked type | In scope. |
| `[Deterministic]` nested inside `[AllowNonDeterminism]` nested inside `[Deterministic]` | Re-enabled — the nearest marking wins at every level. |
| Base type or interface marked, deriving/implementing type not | **Not** in scope. `Inherited = false`, and the analyzer does not walk base types. Interfaces are not even a valid target. |
| `partial` type marked on one part | Whole type is in scope (Roslyn merges attributes across parts). |
| Both attributes on one symbol | `[AllowNonDeterminism]` wins (silence). Contradictory; do not write it. |
| Redundant nesting (`[AllowNonDeterminism]` inside `[AllowNonDeterminism]`) | No diagnostic. |
| Generated code inside a marked scope | Analyzed and reported (`ConfigureGeneratedCodeAnalysis(Analyze \| ReportDiagnostics)`). |

**Detection surface — four operation kinds, nothing else:** invocation, property reference, object creation, method-group reference. Symbol matching is on the `OriginalDefinition` (so generic members and reduced extension-method calls match), never on spelling.

**Catalog contract v1 (fixed, not user-extensible).** A type the compilation does not reference is silently skipped — this is what keeps the package at zero dependencies while still banning `SsalKit.Randomness` entry points when that package *is* referenced.

| Category | Banned members |
|---|---|
| **SSALD001** ambient time | `DateTime.Now`/`.UtcNow`/`.Today`; `DateTimeOffset.Now`/`.UtcNow`; `TimeProvider.System`; `Stopwatch.StartNew()`/`.GetTimestamp()`/`new Stopwatch()`; `Environment.TickCount`/`.TickCount64` |
| **SSALD002** randomness | `Random.Shared`; `new Random()` **and `new Random(seed)`**; `RandomNumberGenerator.Create`/`Fill`/`GetBytes`/`GetNonZeroBytes`/`GetInt32`/`GetHexString`/`GetString`/`GetItems`/`Shuffle`; `Path.GetRandomFileName()`; and — only when referenced — `SsalKit.Randomness.SharedRandomSource.Instance`, `CryptoRandomSource.Instance`, `DeterministicRandom.CreateRandomlySeeded()` |
| **SSALD003** identifiers | `Guid.NewGuid()`; `Guid.CreateVersion7()` (both overloads — the low bits are random even with an explicit timestamp) |
| **SSALD004** hashing | `GetHashCode` **resolving to** `System.Object`, `System.ValueType`, or `System.String`; every member of `System.HashCode`; `StringComparer.GetHashCode(string)` |
| **SSALD005** environment | `Environment.MachineName`/`UserName`/`UserDomainName`/`ProcessId`/`CurrentManagedThreadId`/`ProcessorCount`/`WorkingSet`/`CommandLine`/`CurrentDirectory`/`GetEnvironmentVariable(…)`/`GetEnvironmentVariables(…)`; `Process.GetCurrentProcess()`; `Thread.CurrentThread`; `Path.GetTempPath()`/`GetTempFileName()` |
| **SSALD006** scheduling | `Task.Run`/`Delay`/`WhenAny`/`Yield`; `TaskFactory.StartNew` (incl. `TaskFactory<T>`); `Thread.Sleep`; `ThreadPool.QueueUserWorkItem`; `Parallel.For`/`ForEach`/`Invoke`/`ForAsync`/`ForEachAsync`; `ParallelEnumerable.AsParallel`; `new System.Threading.Timer(…)`; `new System.Timers.Timer(…)` |

**Deliberately *not* banned**, and this is contract, not oversight:

| Not banned | Why |
|---|---|
| `timeProvider.GetUtcNow()` on an **injected** `TimeProvider` | It is the recommended fix. Only the ambient `TimeProvider.System` singleton is banned. |
| `random.Next()` and other instance methods on an existing `Random` | Only where the sequence *comes from* is banned — the creation site, not every draw. |
| A `GetHashCode` call resolving to a **user-written override** | That implementation is analyzed on its own terms if it is in a scope. |
| `new DeterministicRandom(seed)`, `Cooldown`/`TickSchedule`, `ComputeStableHash()` | The replacements this analyzer points at. |
| File/network I/O, `Console`, `await` in general | Out of scope for v1 (catalog size); no diagnostic exists for them. |

## 3. DO NOT

- **DO NOT treat the analyzer's silence as proof that a scope is deterministic.** It sees direct calls only. Zero diagnostics means "no banned API is named here", nothing more.
- **DO NOT expect indirect calls to be caught.** A `[Deterministic]` method calling an unmarked helper that reads the clock reports nothing. Mark the helper types `[Deterministic]` too — that is the intended usage pattern, not a workaround.
- **DO NOT put `[Deterministic]` on an interface** — it is not a valid target and would not reach implementations if it were.
- **DO NOT expect `[Deterministic]` on a base class to cover derived classes.** `Inherited = false` and no base-type walk. Mark each type.
- **DO NOT assume `new Random(seed)` is acceptable because it is seeded.** It is banned on purpose: `System.Random`'s algorithm is not part of its contract and has changed between runtime versions, so a seed does not reproduce a sequence across processes or versions. Use `SsalKit.Randomness.DeterministicRandom`.
- **DO NOT reach for `HashCode.Combine` or `string.GetHashCode()` to build a cache key, shard id, or A/B bucket.** Those seeds are randomized per process. Use `SsalKit.StableHashing`.
- **DO NOT silence a category by deleting the `[Deterministic]` marking.** Use `[AllowNonDeterminism(Justification = "...")]` on the specific member, or set the severity per id in `.editorconfig`.
- **DO NOT leave `[AllowNonDeterminism]` outside a `[Deterministic]` scope.** It suppresses nothing there and reads as a deliberate exemption; that is what `SSALD007` reports.
- **DO NOT expect a code fix.** v1 ships no `CodeFixProvider` — the fixes are refactorings (introducing a `TimeProvider` parameter, threading a seed through), not mechanical edits.
- **DO NOT use this package to ban APIs project-wide.** The opt-in scope is the entire point; `Microsoft.CodeAnalysis.BannedApiAnalyzers` is the tool for a global ban list, and the catalog here is not user-extensible.
- **DO NOT expect a package dependency on SsalKit.Randomness/Timekeeping/StableHashing.** They are named in diagnostic messages and looked up by metadata name; nothing is referenced.

## 4. Diagnostics

Prefix `SSALD`, category `SsalKit.Determinism`. **Every rule is a Warning**, reported only inside a `[Deterministic]` scope.

| ID | Trigger | Fix |
|---|---|---|
| `SSALD001` | Ambient clock or ambient timer read in scope. | Inject a `TimeProvider`, or take the instant as a `DateTimeOffset asOf` argument. |
| `SSALD002` | Process-seeded or cryptographic randomness in scope (including `new Random(seed)`). | `SsalKit.Randomness.DeterministicRandom` (explicit seed, exportable state), or inject an `IRandomSource`. |
| `SSALD003` | `Guid.NewGuid()`/`Guid.CreateVersion7()` in scope. | Derive the id from the data: `ComputeStableHash()` (SsalKit.StableHashing) or bytes from a seeded `DeterministicRandom`. |
| `SSALD004` | Per-process randomized hashing (`object`/`ValueType`/`string` `GetHashCode`, `HashCode`, `StringComparer.GetHashCode`) in scope. | `[StableHashContract]` + `ComputeStableHash()` (SsalKit.StableHashing). |
| `SSALD005` | Machine, process, or thread identity read in scope. | Pass the value in as explicit configuration. |
| `SSALD006` | Scheduling or parallelism API used in scope. | No substitute API — restructure the work to be sequential, or move it outside the scope and feed the result in. |
| `SSALD007` | `[AllowNonDeterminism]` applied where no enclosing symbol has `[Deterministic]`. | Remove the attribute, or mark the enclosing type/member `[Deterministic]`. |

## 5. Canonical snippets

### Mark a deterministic core, and its helpers too

```csharp
using SsalKit.Determinism;
using SsalKit.Randomness;

[Deterministic]
public sealed class BattleSimulation
{
    private readonly DeterministicRandom _random;

    public BattleSimulation(ulong seed) => _random = new DeterministicRandom(seed);

    // Time and randomness both arrive from outside; nothing ambient is read.
    public void Tick(DateTimeOffset asOf) => Resolve(_random.Next(1, 7), asOf);

    private static void Resolve(int roll, DateTimeOffset asOf) { /* ... */ }
}

// The shallow analysis only covers what it can see, so the helper is marked as well --
// otherwise a DateTime.Now added here later would slip through silently.
[Deterministic]
internal static class DamageRules
{
    public static int Apply(int roll, int armor) => Math.Max(0, roll - armor);
}
```

### Exempt a logging path, with a reason

```csharp
using SsalKit.Determinism;

[Deterministic]
public sealed class ReplayRunner
{
    public void Run(DateTimeOffset asOf) { /* ... */ }

    [AllowNonDeterminism(Justification = "wall-clock logging only; never feeds replayed state")]
    private static void LogProgress(int tick) =>
        Console.WriteLine($"{DateTime.UtcNow:O} tick {tick}");
}
```

### Tune severity per category

```ini
# .editorconfig

# Make the deterministic core a build gate...
dotnet_diagnostic.SSALD001.severity = error
dotnet_diagnostic.SSALD002.severity = error
dotnet_diagnostic.SSALD003.severity = error
dotnet_diagnostic.SSALD004.severity = error

# ...while parallelism stays advisory in this codebase.
dotnet_diagnostic.SSALD006.severity = suggestion
```

### Replace a randomized hash key with a stable one

```csharp
using SsalKit.Determinism;
using SsalKit.StableHashing;

[StableHashContract("cache.key", Version = 1)]
public readonly record struct CacheKey
{
    [StableHashMember(1)] public string TenantId { get; init; }
    [StableHashMember(2)] public int Revision { get; init; }
}

[Deterministic]
public static class Buckets
{
    // HashCode.Combine(key.TenantId, key.Revision) would be SSALD004: reshuffles every restart.
    public static int Of(CacheKey key) => (int)(key.ComputeStableHash().Value % 100);
}
```
