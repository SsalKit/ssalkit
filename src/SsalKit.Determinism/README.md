[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Determinism/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Determinism/README.ja.md)

# SsalKit.Determinism

Opt-in compile-time diagnostics for non-deterministic APIs. Mark a type or member `[Deterministic]`, and a bundled analyzer reports every ambient clock, process-seeded random, `Guid.NewGuid()`, randomized hash, environment identifier, and scheduling call written directly inside it — each message naming the concrete replacement to use. Zero dependencies.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Determinism.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Determinism)

## Why SsalKit.Determinism?

Some code has to produce the same output for the same input, every time, on every machine. A lockstep simulation desyncs the moment two clients disagree. A replay stops reproducing the bug it was recorded for. A workflow that re-executes from its history takes a different branch the second time. A cache key computed from `HashCode.Combine` points at a different bucket after a restart. In every one of those cases the defect is a single innocuous line — `DateTime.UtcNow`, `Random.Shared.Next()`, `Guid.NewGuid()` — sitting in code that was never supposed to contain one, and the failure surfaces far away from it, hours later, as a divergence nobody can reproduce.

Nothing in the compiler objects to that line. The BCL cannot: the same `DateTime.UtcNow` is entirely correct in a log statement one file over. What is wrong is not the API, it's the API *in that scope* — and scope is exactly what existing tooling does not express:

- **`Microsoft.CodeAnalysis.BannedApiAnalyzers`** bans an API list across the whole project. Real projects are not shaped that way: a deterministic simulation core, its logging, its UI, and its composition root usually live in one assembly, and a project-wide ban forces either a project split or a wall of suppressions in the code that legitimately reads the clock. It also only ever says "this is banned", never what to do instead.
- **The determinism analyzers that do scope their checks** — Durable Task's, Libplanet's — are tied to their own frameworks and their own notion of what a deterministic region is. They are not usable for a domain service, a pricing calculation, or a game simulation you wrote yourself.

SsalKit.Determinism fills that gap with two properties, and it is worth being precise that these two are the entire product:

- **The scope is opt-in and lexical.** Nothing is reported outside a `[Deterministic]` type or member. You mark the core that has to be reproducible; the code around it is analyzed exactly as before, with no suppressions and no project split. `[AllowNonDeterminism]` carves an exemption back out, and nests both ways.
- **Every message names a concrete replacement.** Not "banned", but *use this instead*: inject a `TimeProvider` or take a `DateTimeOffset asOf` argument, use `DeterministicRandom` with an explicit seed, derive the identifier from the data, replace `HashCode.Combine` with `ComputeStableHash()`. The replacements are the rest of the SsalKit family — and yet **this package depends on nothing**, including them. Their types are looked up by metadata name, so the SsalKit entries in the ban list exist only in a compilation that already references those packages.

The runtime assembly is two attributes and no logic. Everything else happens at compile time, in the analyzer that ships inside the package.

## What this cannot catch — read this before anything else

**The analysis is deliberately shallow: it sees direct calls and nothing else. No diagnostics is not a proof of determinism.** This is an assistive tool, not a guarantee, and it is designed to stay one — "shallow and predictable" is the product, not a limitation waiting to be lifted.

The most important consequence is the first row of this table. A `[Deterministic]` method that calls an unmarked helper, and that helper reads `DateTime.Now`, produces no diagnostic at all:

| Not detected | Why |
|---|---|
| **Indirect calls** — a banned API reached through an unmarked helper | The analyzer never leaves the scope you marked. **Mark the helper types `[Deterministic]` too** — that is the intended usage pattern, not a workaround (see the Quick Start). `Strict = true` checks that you did (see *Strict mode* below); it still never looks inside them. |
| `Dictionary`/`HashSet` enumeration order | Deliberately out of scope: order-dependent consumption of an unordered collection cannot be told apart from order-independent consumption, so any rule here would be mostly false positives. |
| Floating-point differences across platforms (FMA contraction, x87 excess precision, vectorization) | Outside static analysis entirely — the same IL produces different results on different hardware. |
| Culture-dependent formatting and parsing (`ToString()`, `Parse`, `ToUpper`) | Already covered, and covered better, by the BCL's own `CA1304`/`CA1305`/`CA1310`. Enable those rather than expecting this package to duplicate them. |
| Calls dispatched through reflection | The target symbol does not exist at compile time. |
| `await` resumption context and thread affinity | The listed scheduling entry points are caught; the consequences of awaiting are not. |
| Mutable static state, `static` caches, initialization order | Non-determinism that lives in the shape of the program rather than in a named API. |

Two more things follow from the same principle, and both are contract rather than oversight:

- **The scope is where you wrote it.** `[Deterministic]` is `Inherited = false` and the analyzer does not walk base types, so a marked base class does not cover a derived one — mark each type. Interfaces are not a valid target at all, since an attribute there would never reach an implementation.
- **Every rule is a Warning and always will be.** Putting a build-breaking error behind a check that cannot be complete would suggest a completeness it does not have. Raising the severity is a deliberate, per-project decision — see the `.editorconfig` section below.

## Installation

```bash
dotnet add package SsalKit.Determinism
```

The package contains both the attributes and the analyzer that reads them — there is no separate analyzer package to install, and the package has no `PackageReference` of its own.

## Quick Start

Mark the code that has to be reproducible, and the helpers it leans on:

```csharp
using SsalKit.Determinism;
using SsalKit.Randomness;

[Deterministic]
public sealed class BattleSimulation
{
    private readonly DeterministicRandom _random;

    public BattleSimulation(ulong seed) => _random = new DeterministicRandom(seed);

    // Time and randomness both arrive from outside. Nothing ambient is read.
    public int Tick(DateTimeOffset asOf, int armor) => DamageRules.Apply(_random.Next(1, 7), armor);
}

// The analysis only covers what it can see, so the helper is marked as well --
// otherwise a DateTime.Now added here next month would slip through silently.
[Deterministic]
internal static class DamageRules
{
    public static int Apply(int roll, int armor) => Math.Max(0, roll - armor);
}
```

Now let something non-deterministic in, and the build says so:

```csharp
[Deterministic]
public sealed class BattleSimulation
{
    private readonly Random _random = new();                    // SSALD002
    private long _startedAt = DateTime.UtcNow.Ticks;            // SSALD001
    private readonly Guid _runId = Guid.NewGuid();              // SSALD003

    public int Bucket(string playerId) => HashCode.Combine(playerId) % 100;  // SSALD004
}
```

> warning SSALD001: 'DateTime.UtcNow' is non-deterministic: it reads the ambient clock, so the same code produces a different value on every run. Inject a TimeProvider, or take the instant as an argument (the 'DateTimeOffset asOf' parameter shape SsalKit.Timekeeping uses), so the caller decides what time it is

Applied to a type, the scope covers every member of that type **and every nested type**, including the lambdas, local functions, and field or property initializers written inside them. Applied to a method, constructor, or property, it covers that member alone. A `partial` type only needs the attribute on one of its parts.

## The banned-API catalog (v1)

The catalog is fixed and not user-extensible: extending a ban list per project is what `BannedApiAnalyzers` is for, and what this package adds instead is the scope and the advice. Ids are split by category on purpose — the id *is* the tuning knob in `.editorconfig`.

| Id | Category | Banned members | Use instead |
|---|---|---|---|
| `SSALD001` | Ambient time | `DateTime.Now`/`.UtcNow`/`.Today`; `DateTimeOffset.Now`/`.UtcNow`; `TimeProvider.System`; `Stopwatch.StartNew()`/`.GetTimestamp()`/`new Stopwatch()`; `Environment.TickCount`/`.TickCount64` | An injected `TimeProvider` (a `FakeTimeProvider` in tests), or an explicit `DateTimeOffset asOf` parameter — the shape [SsalKit.Timekeeping](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.md) uses throughout. |
| `SSALD002` | Randomness | `Random.Shared`; `new Random()` **and `new Random(seed)`**; `RandomNumberGenerator.Create`/`Fill`/`GetBytes`/`GetNonZeroBytes`/`GetInt32`/`GetHexString`/`GetString`/`GetItems`/`Shuffle`; `Path.GetRandomFileName()`; and, only when that package is referenced, `SsalKit.Randomness`' own `SharedRandomSource.Instance`, `CryptoRandomSource.Instance`, `DeterministicRandom.CreateRandomlySeeded()` | `DeterministicRandom` (explicit seed, exportable state) or an injected `IRandomSource`, from [SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md). |
| `SSALD003` | Identifier generation | `Guid.NewGuid()`; `Guid.CreateVersion7()`, both overloads | Derive the identifier from the data instead: `ComputeStableHash()`, or bytes drawn from a seeded `DeterministicRandom`. |
| `SSALD004` | Randomized hashing | `GetHashCode` **resolving to** `System.Object`, `System.ValueType`, or `System.String`; every member of `System.HashCode`; `StringComparer.GetHashCode(string)` | `[StableHashContract]` + the generated `ComputeStableHash()`, from [SsalKit.StableHashing](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.md). |
| `SSALD005` | Environment identity | `Environment.MachineName`/`.UserName`/`.UserDomainName`/`.ProcessId`/`.CurrentManagedThreadId`/`.ProcessorCount`/`.WorkingSet`/`.CommandLine`/`.CurrentDirectory`/`.GetEnvironmentVariable(…)`/`.GetEnvironmentVariables(…)`; `Process.GetCurrentProcess()`; `Thread.CurrentThread`; `Path.GetTempPath()`/`.GetTempFileName()` | Pass the value in as explicit configuration, so the result depends on its inputs rather than on the host it landed on. |
| `SSALD006` | Scheduling and parallelism | `Task.Run`/`.Delay`/`.WhenAny`/`.Yield`; `TaskFactory.StartNew` (including `TaskFactory<T>`); `Thread.Sleep`; `ThreadPool.QueueUserWorkItem`; `Parallel.For`/`.ForEach`/`.Invoke`/`.ForAsync`/`.ForEachAsync`; `ParallelEnumerable.AsParallel`; `new System.Threading.Timer(…)`; `new System.Timers.Timer(…)` | Nothing drop-in — this is the one category with no substitute API, because the non-determinism is the concurrency itself. Keep genuinely order-independent parallel work outside the scope and feed its result in; otherwise it has to become sequential. |
| `SSALD007` | Orphan exemption | `[AllowNonDeterminism]` where neither the symbol nor anything containing it has `[Deterministic]` | Remove the attribute, or mark the enclosing type or member `[Deterministic]`. A marking that silently does nothing is worse than no marking. |
| `SSALD008` | Missing coverage (**opt-in**, see below) | A member of this assembly, called directly from a `[Deterministic(Strict = true)]` scope, where no `[Deterministic]` sits on it or on any type containing it | Mark it (or its containing type) `[Deterministic]`, carving out members that need the clock with a nested `[AllowNonDeterminism]` — or exempt the *calling* member. A bare `[AllowNonDeterminism]` on the helper is an orphan and silences nothing. |

A few notes on why `new Random(seed)` is on that list, and on what deliberately is **not**:

- **`new Random(seed)` is banned even though it is seeded.** `System.Random`'s algorithm is not part of its documented contract and has already changed between runtime versions, so a fixed seed does not reproduce a sequence across processes or versions — only within one. `DeterministicRandom` pins its algorithm (`xoshiro256**`) as a versioned contract and can export and restore its state.
- **An injected `TimeProvider` is not banned** — it is the recommended fix. Only the ambient `TimeProvider.System` singleton is. Calling `timeProvider.GetUtcNow()` on one you were handed stays silent.
- **Instance methods on an existing `Random` are not banned.** Only where the sequence *comes from* is: the creation site, not every draw.
- **A `GetHashCode` call that resolves to a user-written override is not reported.** Only the framework's own randomized implementations are listed; your override is analyzed on its own terms, in whatever scope it lives in.
- **`nameof(DateTime.UtcNow)` is not reported.** It names a member rather than reading one.
- **File and network I/O, `Console`, and `await` in general are not in the v1 catalog** — a deliberate scope limit, not an endorsement.

The catalog resolves by metadata name once per compilation, and a type the compilation does not reference is silently skipped. That is what lets the `SsalKit.Randomness` rows above coexist with this package's zero-dependency contract: they join the ban list only where that package is already referenced. Their own non-deterministic entry points get no exemption — dogfooding cuts both ways.

## Strict mode: checking that the helpers are marked

Because the analysis only sees direct calls, keeping a deterministic core honest comes down to remembering to mark the helper types it leans on. That is a discipline, and disciplines decay — the helper that was pure when it was written acquires a `DateTime.UtcNow` six months later, and nothing says a word.

`Strict = true` hands that discipline to the compiler:

```csharp
[Deterministic(Strict = true)]
public sealed class ReplayRunner
{
    // SSALD008: no [Deterministic] covers DamageTable, so nothing has ever looked inside it.
    public int Apply(int roll, int armor) => DamageTable.Lookup(roll, armor);
}

internal static class DamageTable
{
    public static int Lookup(int roll, int armor) => Math.Max(0, roll - armor);
}
```

> warning SSALD008: 'DamageTable.Lookup' is called from a [Deterministic(Strict = true)] scope but no [Deterministic] marking covers it, so its body is never analyzed. Mark 'DamageTable' [Deterministic] to bring it into the contract, exempting individual members inside it with [AllowNonDeterminism] where they need it -- or mark the calling member [AllowNonDeterminism] if this call is itself the deliberate non-determinism

**The question it asks is "does a `[Deterministic]` cover this?", not "is this deterministic?"** — it never reads the callee's body. Marking `DamageTable` fixes the example above, and that is the answer nine times out of ten. When a helper genuinely needs the clock, there are two coherent places to say so:

```csharp
// 1. Anchored inside the contract: the type is covered, the one member that needs the
//    clock is carved back out. This is the shape to reach for.
[Deterministic]
internal static class Progress
{
    public static int Percent(int done, int total) => total == 0 ? 0 : done * 100 / total;

    [AllowNonDeterminism(Justification = "console output only; never feeds replayed state")]
    public static void Log(int tick) => Console.WriteLine($"{DateTime.UtcNow:O} tick {tick}");
}

// 2. Caller-side: the call itself is the deliberate non-determinism, exempted exactly the
//    way a direct DateTime.UtcNow would be.
[Deterministic(Strict = true)]
public sealed class ReplayRunner
{
    [AllowNonDeterminism(Justification = "diagnostics path; outside the replayed sequence")]
    private static void Report(int tick) => Telemetry.Emit(tick);
}
```

**What does not work is `[AllowNonDeterminism]` on a standalone helper.** With no `[Deterministic]` above it the attribute suppresses nothing — that is exactly what `SSALD007` reports about it — so it cannot be what silences `SSALD008` either. Both rules run off the same coverage question, so you get both diagnostics, pointing the same way, instead of one quietly cancelling the other. It is also why an exemption never reaches out of a callee to silence call sites it cannot see: the exemption lives where the decision was made.

**It does not make the analysis any deeper, and it is not the interprocedural propagation this package will never do.** The callee's body is never read; the check is exactly one hop and its only input is where the markings sit. Everything in the *What this cannot catch* section above still holds with strict mode on — silence still is not a proof of determinism. What changes is that the manual discipline behind the first row of that table is now checked instead of trusted.

Two consequences worth knowing before you switch it on:

- **It is opt-in per scope, not per project.** A scope-level switch is the right granularity: a solution usually has one replay path or one simulation core that earns this, while the rest of its deterministic code is better served by the seven catalog rules alone. It is also deliberately *not* on by default — this rule reports an absence rather than a named API, so it is the noisiest of the eight, and one noisy rule is how a whole category ends up disabled in `.editorconfig`.
- **It pushes markings towards types rather than members.** Marking a single method strict makes its own type's private helpers reportable; marking the type silences them. That is intended: the natural unit of a determinism contract is a type, and a type is where its helpers usually live.

Strict is part of the scope, so it obeys the same nearest-wins rule as everything else — a nested `[Deterministic]` without `Strict` turns it off inside that nested scope, which is how you relax it locally.

Nothing you cannot fix is ever reported. Other assemblies (the BCL, and the other SsalKit packages — they do not reference this one and never will), interface members, compiler-synthesized members, source-generated code, positional records, auto-implemented properties, `abstract` and `extern` declarations, and fields are all left alone, because in each case there is either nowhere to write the attribute or nothing behind it to analyze.

> Source-generated callees are worth calling out, because they are the exclusion you are most likely to meet: a generated extension class — `ComputeStableHash()` among them — is exactly the kind of helper a deterministic core calls, and no attribute can be written into a file the build regenerates. A generator that emits *into* your own `[Deterministic] partial` type is a different thing entirely and was always covered: that code is inside your scope, and it is analyzed there.

## Exempting code you meant to write

Some code inside a deterministic core genuinely needs the clock — a log line, a diagnostic counter, a progress message. There are two ways to say so, and they are meant for different sizes of problem:

**1. `[AllowNonDeterminism]` on the member or nested type (preferred).** It names the whole member rather than one call site, it shows up in the declaration where a reviewer is already looking, and `Justification` carries the reason into code review. Nothing reads it at run time and no diagnostic requires it — but a bare exemption tells the next reader nothing.

```csharp
[Deterministic]
public sealed class ReplayRunner
{
    public void Run(DateTimeOffset asOf) { /* analyzed */ }

    [AllowNonDeterminism(Justification = "wall-clock logging only; never feeds replayed state")]
    private static void LogProgress(int tick) =>
        Console.WriteLine($"{DateTime.UtcNow:O} tick {tick}");
}
```

Scope resolution is lexical and nearest-wins, so exemptions nest in both directions: an `[AllowNonDeterminism]` type inside a `[Deterministic]` one is exempt, and a `[Deterministic]` member inside *that* is analyzed again. Outside every `[Deterministic]` scope the attribute suppresses nothing, which is what `SSALD007` reports.

**2. `#pragma warning disable` / `.editorconfig`, for a single call site.**

```csharp
#pragma warning disable SSALD001 // one-off: seeding the trace id, not simulation state
var traceStartedAt = DateTime.UtcNow;
#pragma warning restore SSALD001
```

Deleting the `[Deterministic]` marking to silence a category is the one thing not to do: it silences every future violation in that scope too, and leaves no record that it was ever deliberate.

## Tuning severity in `.editorconfig`

Every rule ships as a Warning. Because the rules are split across eight ids by category, tightening or relaxing one category is a single line:

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

All eight share one category, so they can also be moved together:

```ini
dotnet_analyzer_diagnostic.category-SsalKit.Determinism.severity = error
```

Note that a `.editorconfig` can be scoped by path (`[src/Simulation/**.cs]`), which pairs well with a solution where one project holds the deterministic core.

There is no code fix provider in v1: the fixes here are refactorings — introducing a `TimeProvider` parameter, threading a seed through a constructor — rather than mechanical edits.

## Where this pays off

Each of the first, second, fifth, and sixth items below is a runnable section of [samples/SsalKit.Determinism.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Determinism.Sample), whose section names match this list.

- **Lockstep simulation** (`[Simulation]`, `[Desync]`). Clients that simulate the same world from the same inputs must stay bit-identical; one wall-clock read on one machine is a desync. Marking the simulation core `[Deterministic]` turns that class of bug from a runtime mystery into a build warning — and with `TreatWarningsAsErrors`, into something that cannot be committed.
- **Replay and event-sourced verification** (`[Replay]`). A recorded input sequence has to reproduce the original run exactly, or the recording is worthless as a bug report and as an audit trail. The whole replay path is the scope to mark.
- **Workflow re-execution.** Durable-execution engines (Durable Functions, Temporal, and friends) replay a workflow's history through the same code and require every decision to come out the same way. Those frameworks ship their own analyzers for their own attributes; when re-execution logic of your own lives outside a framework's marked region — a saga step, a retry planner — `[Deterministic]` is how you get the same guardrail.
- **Consensus and distributed agreement.** Independent nodes must reach identical conclusions from identical inputs. `Guid.NewGuid()` or `Environment.MachineName` inside the deciding path makes agreement impossible, and the failure appears as a rare, unreproducible split rather than as an obvious bug.
- **Cache keys, sharding, and A/B bucketing** (`[Fingerprint]`). `HashCode.Combine` and `string.GetHashCode()` are randomized per process, so a key or a bucket computed today is a different key or bucket after the next restart. `SSALD004` catches exactly this, and points at the stable replacement.
- **A testable domain core** (`[TestableCore]`, `[OptOut]`). The everyday case: a service that takes its time and its randomness from outside is a service whose tests do not need a mock clock, a retry, or a `Thread.Sleep`. `[Deterministic]` turns "we agreed to inject time" from a code-review convention into something the compiler enforces.

What the sample deliberately does *not* print is a single warning: it compiles under `TreatWarningsAsErrors`, which is the proof that no `SSALD` diagnostic fires anywhere in it. The opposite demonstration — one violation from every category, each annotated with its replacement — lives in its `Violations.cs`, excluded from the default build behind an `#if`; the `[Showcase]` group explains how to switch it on.

## The rest of the family

The replacements named in the diagnostics are separate, optional packages — this one depends on none of them, and works without any of them installed:

- **[SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md)** — `DeterministicRandom` (`xoshiro256**`, explicit seed, exportable and forkable state) and the `IRandomSource` abstraction, for `SSALD002`.
- **[SsalKit.Timekeeping](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.md)** — calendar reset boundaries, cooldowns and recharging pools, and a logical-tick event schedule, all computed from an instant you pass in rather than one they read, for `SSALD001`.
- **[SsalKit.StableHashing](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.md)** — `[StableHashContract]` and a generated `ComputeStableHash()` producing checksums that survive processes, machines, and .NET versions, for `SSALD003` and `SSALD004`.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
