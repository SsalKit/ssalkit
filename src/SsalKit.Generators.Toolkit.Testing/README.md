[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit.Testing/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit.Testing/README.ja.md)

# SsalKit.Generators.Toolkit.Testing

A thin, test-framework-agnostic harness for testing incremental source generators and analyzers: runs a generator over in-memory source, proves the generated code recompiles, asserts diagnostics by id/severity/location — and asserts the one thing snapshots can never catch, that your pipeline still caches.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.Testing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit.Testing)

## Why SsalKit.Generators.Toolkit.Testing?

Every source generator test project starts by writing the same forty lines. Build a `CSharpCompilation` from a string. Work out which reference assemblies to hand it, so that generated code is type-checked against the real BCL rather than against a stub the test declared. Create a `CSharpGeneratorDriver`. Run it. Dig the generated sources out of `GetRunResult().Results.SelectMany(...)`. Remember to run the *output* compilation's diagnostics too, because a generator that emits syntactically valid code that doesn't type-check will otherwise pass every assertion you wrote. None of that is interesting, all of it is the same in every project, and every one of those projects re-derives it from a blog post.

That is the boring half. The other half is the reason this package exists:

**Nothing verifies that your generator is still incremental.** An incremental generator's pipeline caches by value equality. Put an `ISymbol`, a `Location`, a raw `ImmutableArray<T>`, or anything else with reference equality into a pipeline model, and the cache silently stops working — every stage recomputes on every keystroke, and the generator that ran in 3 ms in your tests now re-runs the whole pipeline every time a consumer types a character in their IDE. Your snapshot tests keep passing, because the *output* is still correct. Only the cost changed.

Roslyn does expose the evidence — `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)` records a per-step reason (`Cached`, `Unchanged`, `Modified`, `New`) for every output — but reading `TrackedSteps` correctly means driving two runs on the same driver, changing the compilation in between, and knowing which reasons prove what. Almost nobody does it, and no packaged harness offers it as an assertion.

This one does:

- **`IncrementalAssert.AllCachedOrUnchanged`** — after an edit your pipeline's models don't observe, nothing may be recomputed. This is the assertion that fails the moment a model loses value equality.
- **`IncrementalAssert.SomeOutputRecomputed`** — after an edit they *do* observe, something must be recomputed. This is the assertion that fails when a model drops a field the emitter actually uses, which would otherwise keep serving a stale output forever.

Together they're a two-sided contract: a model that captures too much fails the first, a model that captures too little fails the second. Step tracking is always on, so both are available on every run without opting in up front.

Everything else in the package is the boilerplate you no longer have to write:

- **A generator that crashes fails the test.** Roslyn never lets an exception out of a generator or an analyzer: it catches it and records it — as `CS8785`, a *warning*, for a generator, as `AD0001` for an analyzer. Neither is an error, so a crashed run leaves a compilation that still compiles cleanly, no generated files, and none of your package's diagnostics — and `AssertNoGeneratedSources()`, `DiagnosticAssert.None(...)` and "no error was reported" all pass, every one of them for the wrong reason. This harness refuses to hand such a run back at all.
- **Real references, by default.** The compilation under test is built against every reference assembly the test host itself trusts, so `AssertCompilesCleanly()` type-checks generated code against the actual BCL. `AdditionalAssemblies = [typeof(MyAttribute).Assembly]` adds your shipping runtime package to that, so the emitted calls are checked against the API you actually ship, not against a copy of it pasted into the test source.
- **Assertions that read like the intent.** `GetSingleSource()`, `GetSource("...ServiceCollectionExtensions.g.cs")`, `AssertNoGeneratedSources()`, `DiagnosticAssert.Single(..., exclusive: true)`, `DiagnosticAssert.LocatedOn(diagnostic, "[Marker]")` — and failure messages that list what *was* generated, or what *was* reported, instead of `Expected 1, got 0`.
- **Analyzers too.** The same compilation setup runs a whole package's analyzers together, which is what proves the other analyzers stay silent about whichever construct a test source uses.

## Installation

```bash
dotnet add package SsalKit.Generators.Toolkit.Testing
```

This is a **test-project** package: reference it from the test project, not from the generator project you ship.

```xml
<ItemGroup>
  <PackageReference Include="SsalKit.Generators.Toolkit.Testing" Version="0.1.0" />
</ItemGroup>
```

## Prerequisites

- Your test project targets **`net10.0`** or later. The package targets `net10.0` alone: it is consumed by test projects, which are free to move to the current TFM in a way a shipped library is not, and single-targeting keeps the harness free of `#if` and of a second set of behaviours to reason about. Multi-targeting is a backlog item, not a decision against it — open an issue if an older test TFM is blocking you.
- The package brings **`Microsoft.CodeAnalysis.CSharp`** with it — that is its only dependency, and it deliberately does not depend on any test framework.

## Quick start

### Run a generator and check what it produced

```csharp
using SsalKit.Generators.Toolkit.Testing;

[Fact]
public void EmitsAGreeterForEachMarkedType()
{
    var result = GeneratorTest.Run<GreeterGenerator>(
        """
        namespace Demo;

        [Mine.Marker("hello")]
        public sealed class Widget;
        """);

    string generated = result.AssertCompilesCleanlyAndGetSource();

    Assert.Contains("public static class WidgetGreeter", generated);
}
```

`AssertCompilesCleanly()` re-checks the compilation *with* the generated sources in it. It is the assertion that separates "the emitted text looks right" from "the emitted text is valid C# that binds against the API it calls into" — and it is the one most hand-rolled harnesses forget. It returns the result, so it chains straight into whichever lookup follows; `AssertCompilesCleanlyAndGetSource()` is a name for the one pair almost every single-output generator test opens with.

When a run produces several files, `GetSource("...Extensions.g.cs")` picks one out by a suffix of its hint name, and `ToSnapshotText()` renders all of them into a single string (each preceded by a `// ==== <hint name>` line) to hand to a snapshot library:

```csharp
[Fact]
public Task WholeRun_MatchesSnapshot()
{
    var result = GeneratorTest.Run<GreeterGenerator>(Source, Options);

    return Verify(result.AssertCompilesCleanly().ToSnapshotText());
}
```

### Share the compilation setup

Keep one `static readonly` options instance per test project, so a single place decides what the generated code is type-checked against:

```csharp
internal static class GeneratorTestSupport
{
    public static readonly GeneratorTestOptions Options = new()
    {
        // Only this package's own diagnostics reach an assertion, so a deliberately invalid
        // test source's incidental compiler errors never have to be filtered out by hand.
        DiagnosticIdPrefix = "MINE",

        // A multi-file snapshot cannot churn because production order changed.
        SortGeneratedSourcesByHintName = true,

        // The generated code is checked against the shipping runtime package.
        AdditionalAssemblies = [typeof(Mine.MarkerAttribute).Assembly],
    };
}
```

Every entry point takes options (or `null` for `GeneratorTestOptions.Default`), and it is an immutable record, so a one-off variant is a `with` expression: `Options with { AllowUnsafe = true }`.

### Assert the caching contract

```csharp
using Microsoft.CodeAnalysis.CSharp;

[Fact]
public void AnUnrelatedEditRecomputesNothing()
{
    var (_, second) = GeneratorTest.RunTwiceWithCompilationChange<GreeterGenerator>(
        Source,
        static compilation => compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// nothing to do with the generator")));

    IncrementalAssert.AllCachedOrUnchanged(
        second, TrackingNames.Models, TrackingNames.Collected);
}

[Fact]
public void AnEditTheModelCapturesFlowsThroughToTheOutput()
{
    var (first, second) = GeneratorTest.RunTwice<GreeterGenerator>(
        Source, static source => source.Replace("\"hello\"", "\"goodbye\""));

    Assert.Contains("hello", first.GetSingleSource());
    Assert.Contains("goodbye", second.GetSingleSource());

    IncrementalAssert.SomeOutputRecomputed(second, TrackingNames.Models);
}
```

Both take the **second** of two runs sharing one driver — `RunTwice` replaces the source file, `RunTwiceWithCompilationChange` hands you the compilation so you can add or replace a syntax tree. The tracking names are whatever your pipeline passed to `WithTrackingName`; if one was never recorded, the failure message lists the names the run *did* record, which is usually enough to spot the typo.

The **output** stage can be named too, as `"SourceOutput"`. It takes no `WithTrackingName` of its own and Roslyn records it in a dictionary of its own (`GeneratorTestResult.TrackedOutputSteps`), so it is the part of a pipeline easiest never to look at — and it is the one that decides whether your emitter actually re-runs. Value stages reporting `Unchanged` while `"SourceOutput"` reports `Modified` means the emitting is happening on every keystroke after all:

```csharp
IncrementalAssert.AllCachedOrUnchanged(second, TrackingNames.Models, "SourceOutput");
```

What the incremental assertions **cannot** see is retention. The reasons Roslyn records answer "did this step recompute", not "what is this step's value holding on to", so a model that compares by value while keeping an `ISymbol` or a `Compilation` alive in a field equality ignores passes both assertions and still pins whole compilations in the driver's cache. Keeping symbols and syntax out of pipeline models stays a design rule, not a tested one.

Note what `RunTwice` can and cannot express: it replaces the whole source file, so a mutated second source invalidates every syntax-driven stage by construction. Called *without* a mutation it re-parses the identical text, which is the strictest caching test there is — nothing the pipeline observes changed, so nothing may recompute. But to assert that an edit *somewhere else in the compilation* changes nothing — the realistic IDE scenario — use `RunTwiceWithCompilationChange` and add an unrelated tree, as above.

When an assertion fails it prints the per-step cache state, which is what turns "the cache broke" into "`Models[0] -> Modified`":

```
Expected every output of step 'Models' to be Cached or Unchanged after the second run,
but 1 of them was recomputed.

Cache state of the requested steps:
  Models[0] -> Modified

Tracking names recorded by this run:
  - Collected
  - Models
```

### Assert diagnostics

```csharp
[Fact]
public void AMarkerOnAStructIsRejected()
{
    const string source = """
        namespace Demo;

        [Mine.Marker("hello")]
        public struct Widget;
        """;

    var result = GeneratorTest.Run<GreeterGenerator>(source, Options);

    var diagnostic = DiagnosticAssert.Single(
        result.Diagnostics, "MINE001", DiagnosticSeverity.Error, exclusive: true);

    Assert.Contains("Widget", diagnostic.GetMessage());
    DiagnosticAssert.LocatedOn(diagnostic, """[Mine.Marker("hello")]""", source);
}
```

- `Single` asserts that exactly one diagnostic has that id, optionally checks its severity and location, and returns it so the message can be asserted too. `exclusive: true` additionally asserts it was the *only* diagnostic reported — use it whenever the test source is supposed to trigger exactly one thing, so a second, unexpected diagnostic can't slip through beside the expected one.
- `LocatedOn` names a position by a **snippet of the source** rather than by a line and column, so the assertion stays readable and doesn't drift when the source above it is edited. The snippet must occur exactly once; the diagnostic's span must fall inside it. Generator diagnostics rebuilt from a cache-safe location record carry no syntax tree, so those need the source passed in (as above); analyzer diagnostics don't.
- `SpanStartsWith(diagnostic, "Mine.Marker", source)` is the variant for when no snippet is unique — the same attribute applied to two members of one type, say, where every snippet naming it occurs twice. It matches the prefix against the reported span itself instead of searching the source for it, so it pins down where the span begins and what it begins with, but not how far it reaches. Prefer `LocatedOn` when a unique snippet exists, since that one checks the extent too.
- `None(diagnostics, "MINE")` asserts that *nothing* with that prefix was reported — which also catches a diagnostic nobody thought to name in the test.

Analyzers run through the same setup, as a set:

```csharp
var diagnostics = await GeneratorTest.RunAnalyzersAsync(
    source, [new MarkerAnalyzer(), new NamingAnalyzer()], Options);

DiagnosticAssert.None(diagnostics, "MINE");
```

## API overview

| Type | What it does |
|------|--------------|
| `GeneratorTest` | The entry points. `Run<TGenerator>` for a single run; `RunTwice<TGenerator>`/`RunTwiceWithCompilationChange<TGenerator>` for the two-run pair the incremental assertions consume; `RunAnalyzerAsync<TAnalyzer>`/`RunAnalyzersAsync` for analyzers; `CreateCompilation` to build the compilation without running anything; `CompileToReference` to compile a second, separate assembly to reference. |
| `GeneratorTestOptions` | The shared knobs, as an immutable record: `AssemblyName`, `LanguageVersion`, `NullableContextOptions`, `OutputKind`, `AllowUnsafe`, `AdditionalReferences`, `AdditionalAssemblies`, `DiagnosticIdPrefix`, `SortGeneratedSourcesByHintName`, `AllowGeneratorExceptions`. `GeneratorTestOptions.Default` is what `null` means. |
| `GeneratorTestResult` | What one run produced. Data: `GeneratedSources`, `Diagnostics`, `OutputCompilation`, `RawResult`, `TrackedSteps`, `TrackedOutputSteps`. Lookups: `GetSingleSource()`, `GetSource(hintNameSuffix)`, `GetCompilationErrors()`, `ToSnapshotText()`. Assertions: `AssertCompilesCleanly()`, `AssertCompilesCleanlyAndGetSource()`, `AssertNoGeneratedSources()`. |
| `GeneratedSource` | One generated file: a `readonly record struct` of `HintName` and `Text`. |
| `IncrementalAssert` | The caching contract: `AllCachedOrUnchanged(secondRun, ...names)` and `SomeOutputRecomputed(secondRun, ...names)`. |
| `DiagnosticAssert` | `Single(diagnostics, id, severity?, locatedOnSnippet?, source?, exclusive?)`, `None(diagnostics, idPrefix)`, `LocatedOn(diagnostic, snippet, source?)`, `SpanStartsWith(diagnostic, prefix, source?)`. |
| `GeneratorAssertionException` | The failure signal every assertion throws, carrying the diagnosis in its message. |

## Test-framework independence

The package references no test framework. A failed assertion throws `GeneratorAssertionException`; every framework treats an unhandled exception from a test as a failure, so the same harness works unchanged under xunit, NUnit, MSTest, and TUnit — and a repository that uses two of them doesn't end up with two flavours of the harness.

The trade-off is that a failure surfaces as an exception rather than as your framework's native assertion type, so the message has to carry the diagnosis on its own. That is deliberately where the effort went: failures list the hint names of every file that *was* generated, the compiler errors from the regenerated compilation, the id/severity/position/message of every diagnostic that *was* reported, or the per-step cache state — not a bare expected-versus-actual.

Nothing stops you from mixing in your framework's assertions for the parts it does better (`Assert.Contains` on generated text, `[Theory]` data). The harness only owns the assertions that are about generators.

## How this relates to `Microsoft.CodeAnalysis.Testing`

It isn't a replacement, and it isn't trying to be one. The two solve different problems:

| | `Microsoft.CodeAnalysis.Testing` | This package |
|---|---|---|
| Built for | Analyzers and **code fixes** | Incremental **source generators** |
| Shape | A configurable `*Test` fixture with `TestState`/`FixedState`, markup syntax, per-framework packages | Plain static methods returning a result object |
| Diagnostics | Declared as expected state, verified by the fixture | Asserted explicitly, after the run |
| Code fix / refactoring verification | Yes — the reason to use it | No |
| Multi-project, additional files, `.editorconfig`, analyzer config | Extensive support | Out of scope |
| Incremental caching (`trackIncrementalGeneratorSteps`) | No | **Yes** — the reason to use this |

If you're testing an analyzer *with a code fix*, use `Microsoft.CodeAnalysis.Testing`; nothing here replaces `VerifyCodeFixAsync`. If you're testing an incremental generator — its emitted code, its diagnostics, and whether it still caches — this package is a much smaller thing to learn and covers the one thing the other doesn't. Using both in one repository is fine; they share no state and no configuration.

## Gotchas

### `CompileToReference` overrides `AssemblyName`

`CompileToReference(source, assemblyName, options)` compiles a second, separate assembly for tests that need cross-assembly rules (`internal` accessibility, `extern alias`, `protected internal`, `[InternalsVisibleTo]`). It reuses everything in `options` — references, language version, nullable context — **except** `AssemblyName`, which the `assemblyName` parameter always overrides:

```csharp
// Options.AssemblyName is "MyApp.Sample"; the reference assembly is named "Contracts", not that.
var contracts = GeneratorTest.CompileToReference(ContractsSource, "Contracts", Options);

var result = GeneratorTest.Run<GreeterGenerator>(
    source, Options with { AdditionalReferences = [contracts] });
```

That's what you want (two assemblies in one compilation can't share a name), but it means a test whose generator keys on the assembly name — to name the file or the extension class it emits, say — must not assume the options it passed here decided that name. Name the reference assembly through the parameter and read it there.

### `SortGeneratedSourcesByHintName` and snapshots

`GeneratedSources` is in production order by default, which is whatever order your pipeline emitted the files in. That order is stable in practice but not part of any contract, so a snapshot covering several files at once should set `SortGeneratedSourcesByHintName = true` and stop depending on it. Per-file lookups (`GetSource`, `GetSingleSource`) don't care either way.

### `DiagnosticIdPrefix` filters, `RawResult` doesn't

`GeneratorTestResult.Diagnostics` and the analyzer entry points honour `DiagnosticIdPrefix`, which is what lets a test source be deliberately invalid without every assertion having to filter out the resulting `CS****`. The unfiltered generator diagnostics are still there on `RawResult.Diagnostics`, and `OutputCompilation`/`GetCompilationErrors()` are unaffected by it.

Two ids are exempt: `CS8785` and `AD0001` survive the filter whatever the prefix is. The prefix exists to drop incidental `CS****` noise, but the one `CS****` that says "your generator crashed" is not noise, and dropping it silently is precisely how a crashed run passes a test.

### Testing a crash on purpose

`AllowGeneratorExceptions = true` turns the crash check off and hands the run back as it is — for a generator whose contract *is* to fail loudly on some input, or for a test of this behaviour itself:

```csharp
var result = GeneratorTest.Run<MyGenerator>(source, Options with { AllowGeneratorExceptions = true });

Assert.Equal("CS8785", Assert.Single(result.Diagnostics).Id);
Assert.IsType<InvalidOperationException>(Assert.Single(result.RawResult.Results).Exception);
```

Without it, the same run throws a `GeneratorAssertionException` naming the generator, the exception type, its message, and its stack trace.

### `ToSnapshotText()` always joins with `"\n"`

The `// ==== <hint name>` headers and the joins between files are line feeds, never `Environment.NewLine`. A snapshot is written on one machine and compared on another, so a host-dependent separator would turn "the generator's output changed" into "the test ran somewhere else". Line breaks *inside* a generated file are whatever your generator emitted — that is your generator's contract, and worth fixing to `"\n"` there too.

## Relationship to SsalKit.Generators.Toolkit

[SsalKit.Generators.Toolkit](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.md) is the other half of the same job: it's what you write a generator *with* (equatable arrays, an indented code writer, C# naming helpers, hint-name sanitization, cache-safe diagnostic descriptions), this is what you test one *with*. They pair naturally — `EquatableArray<T>` and `DiagnosticInfo` exist so that pipeline models compare by value, and `IncrementalAssert` is how you prove they actually did — but neither depends on the other, and either works on its own. The Toolkit is a source-only package embedded into a `netstandard2.0` generator project; this is an ordinary `net10.0` assembly referenced by a test project.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
