# SsalKit.Generators.Toolkit.Testing — AI contract sheet

A framework-agnostic in-process harness for testing incremental source generators and analyzers: run a generator over in-memory source, prove the generated code recompiles, assert diagnostics by id/severity/location, and assert the **incremental caching** contract that snapshots cannot see.

- **TFM:** `net10.0` only (no multi-targeting). **Package dependency:** `Microsoft.CodeAnalysis.CSharp` (its only one).
- **Test-project package.** Reference it from the test project, never from the generator project you ship.
- **Namespace:** `SsalKit.Generators.Toolkit.Testing`.
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 1. API surface

### Pick the right entry point

| Requirement | Use |
|---|---|
| Run a generator once, inspect the output | `GeneratorTest.Run<TGenerator>(source, options)` |
| Prove the emitted code type-checks | `result.AssertCompilesCleanly()` (or `AssertCompilesCleanlyAndGetSource()`) |
| Prove an input is deliberately ignored | `result.AssertNoGeneratedSources()` |
| Assert caching after an edit the pipeline ignores | `RunTwiceWithCompilationChange` + `IncrementalAssert.AllCachedOrUnchanged` |
| Assert caching after an edit the pipeline observes | `RunTwice` + `IncrementalAssert.SomeOutputRecomputed` |
| Snapshot the whole run, hint names included | `result.ToSnapshotText()` + `SortGeneratedSourcesByHintName = true` |
| Run analyzers (as a set) | `GeneratorTest.RunAnalyzersAsync(source, analyzers, options)` |
| Cross-assembly rules (`internal`, `extern alias`, `[InternalsVisibleTo]`) | `GeneratorTest.CompileToReference` → `AdditionalReferences` |
| Type-check generated code against the real shipping package | `AdditionalAssemblies = [typeof(MyAttribute).Assembly]` |
| A generator whose contract *is* to crash | `AllowGeneratorExceptions = true` |

### `GeneratorTest` — `static class`

| Member | Contract |
|---|---|
| `CSharpCompilation CreateCompilation(string source, GeneratorTestOptions? options = null)` | One syntax tree + the test host's trusted reference assemblies + `AdditionalReferences`/`AdditionalAssemblies`. Runs nothing. |
| `MetadataReference CompileToReference(string source, string assemblyName, GeneratorTestOptions? options = null)` | Emits a second in-memory assembly. **`assemblyName` always overrides `options.AssemblyName`.** Throws `GeneratorAssertionException` on compile failure. |
| `GeneratorTestResult Run<TGenerator>(string source, GeneratorTestOptions? options = null)` | One run. `TGenerator : IIncrementalGenerator, new()`. |
| `(GeneratorTestResult First, GeneratorTestResult Second) RunTwice<TGenerator>(string source, Func<string, string>? mutateForSecondRun = null, GeneratorTestOptions? options = null)` | Two runs on **one driver**, replacing the whole source file. `null` mutation re-parses identical text — the strictest caching test. |
| `(GeneratorTestResult First, GeneratorTestResult Second) RunTwiceWithCompilationChange<TGenerator>(string source, Func<Compilation, Compilation> changeForSecondRun, GeneratorTestOptions? options = null)` | Two runs on one driver; you change the compilation (typically add a tree). |
| `Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(string source, GeneratorTestOptions? options = null)` | Single analyzer. |
| `Task<ImmutableArray<Diagnostic>> RunAnalyzersAsync(string source, IEnumerable<DiagnosticAnalyzer> analyzers, GeneratorTestOptions? options = null)` | Several analyzers together. `ArgumentException` when empty. |

Incremental step tracking (`trackIncrementalGeneratorSteps: true`) is **always on** — no opt-in needed.

### `GeneratorTestOptions` — `sealed record`

| Property | Default | Contract |
|---|---|---|
| `string AssemblyName` | `"TestAssembly"` | Set it deliberately when the generator keys on the assembly name. |
| `LanguageVersion LanguageVersion` | `Latest` | Parses both the test source and generator-added source. |
| `NullableContextOptions NullableContextOptions` | `Enable` | What makes `AssertCompilesCleanly` meaningful for annotated generated code. |
| `OutputKind OutputKind` | `DynamicallyLinkedLibrary` | Use `ConsoleApplication` for a source with an entry point. |
| `bool AllowUnsafe` | `false` | |
| `ImmutableArray<MetadataReference> AdditionalReferences` | `[]` | A `default` array is normalized to empty. |
| `ImmutableArray<Assembly> AdditionalAssemblies` | `[]` | Reference already-loaded assemblies by `typeof(X).Assembly`. Normalized like above. |
| `string? DiagnosticIdPrefix` | `null` | Filters `GeneratorTestResult.Diagnostics` and the analyzer entry points. |
| `bool SortGeneratedSourcesByHintName` | `false` | Orders `GeneratedSources` by hint name instead of production order. |
| `bool AllowGeneratorExceptions` | `false` | When `false` a generator/analyzer crash is a failed assertion. |
| `static GeneratorTestOptions Default { get; }` | — | What `null` means. Build variants with `with`. |

### `GeneratorTestResult` — `sealed class`

| Member | Contract |
|---|---|
| `ImmutableArray<GeneratedSource> GeneratedSources` | Production order, or hint-name order when sorted. |
| `ImmutableArray<Diagnostic> Diagnostics` | Filtered by `DiagnosticIdPrefix`; `CS8785`/`AD0001` always survive. |
| `Compilation OutputCompilation` | With the generated sources added. |
| `GeneratorDriverRunResult RawResult` | Escape hatch: unfiltered diagnostics, `Exception`, everything not wrapped. |
| `ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> TrackedSteps` | The **value** pipeline, keyed by `WithTrackingName`. |
| `ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> TrackedOutputSteps` | The **output** stages, keyed by the well-known name (`"SourceOutput"`). |
| `ImmutableArray<Diagnostic> GetCompilationErrors()` | Error-severity diagnostics of `OutputCompilation`. |
| `string GetSingleSource()` | Throws unless exactly one file was produced; the message lists the hint names. |
| `string GetSource(string hintNameSuffix)` | Exactly one hint name must end with the suffix. |
| `string ToSnapshotText()` | Every file, each preceded by `// ==== <hint name>`, joined with `"\n"`. |
| `GeneratorTestResult AssertCompilesCleanly()` | Returns `this` for chaining. |
| `string AssertCompilesCleanlyAndGetSource()` | `AssertCompilesCleanly().GetSingleSource()`. |
| `GeneratorTestResult AssertNoGeneratedSources()` | Returns `this`. |

### `IncrementalAssert` — `static class`

| Member | Contract |
|---|---|
| `void AllCachedOrUnchanged(GeneratorTestResult secondRun, params string[] trackingNames)` | Every output of every named step must be `Cached` or `Unchanged`. |
| `void SomeOutputRecomputed(GeneratorTestResult secondRun, params string[] trackingNames)` | At least one output of each named step must be `Modified` or `New`. |

Both take the **second** run of a two-run pair sharing a driver. Names resolve against `TrackedSteps` **and** `TrackedOutputSteps` (value table wins a collision), so `"SourceOutput"` can be named. Empty `trackingNames` → `ArgumentException`; an untracked name → `GeneratorAssertionException` listing every name the run did record.

### `DiagnosticAssert` — `static class`

| Member | Contract |
|---|---|
| `Diagnostic Single(ImmutableArray<Diagnostic> diagnostics, string id, DiagnosticSeverity? severity = null, string? locatedOnSnippet = null, string? source = null, bool exclusive = false)` | Exactly one diagnostic with that id; optional severity and location checks; `exclusive: true` also requires it to be the only diagnostic reported. Returns it. |
| `void None(ImmutableArray<Diagnostic> diagnostics, string idPrefix)` | Nothing with that prefix was reported. |
| `void LocatedOn(Diagnostic diagnostic, string snippet, string? source = null)` | The snippet must occur **exactly once** in the source and must span the reported location. |
| `void SpanStartsWith(Diagnostic diagnostic, string prefix, string? source = null)` | Matches the prefix against the reported span itself; use when no snippet is unique. Weaker — it does not check the extent. |

### `GeneratedSource` / `GeneratorAssertionException`

| Type | Contract |
|---|---|
| `readonly record struct GeneratedSource(string HintName, string Text)` | `HintName` includes the extension (Roslyn appends `.cs` when the generator omits it). |
| `sealed class GeneratorAssertionException : Exception` | The single failure signal. Constructor takes the message only. |

## 2. Contracts (versioned / immutable)

- **A crash is a failed assertion by default.** Roslyn never lets an exception escape a generator or an analyzer: it records it as `CS8785` (a **warning**) for a generator and `AD0001` for an analyzer. Neither is an error, so a crashed run still "compiles cleanly", produces no files, and reports none of the package's own diagnostics — and `AssertNoGeneratedSources()`, `DiagnosticAssert.None(...)`, and "no error was reported" all pass for the wrong reason. Every entry point therefore refuses to return such a run unless `AllowGeneratorExceptions` is set.
- **`DiagnosticIdPrefix` never filters `CS8785` or `AD0001`**, whatever it is set to. The prefix exists to drop incidental `CS****` noise from deliberately invalid sources; the one `CS****` meaning "your generator crashed" is not noise.
- **No test-framework dependency.** Failures are `GeneratorAssertionException`, which every framework treats as a failed test. The message carries the diagnosis (hint names produced, compiler errors, reported diagnostics, or per-step cache state).
- **Step tracking is always enabled**, so `TrackedSteps`/`TrackedOutputSteps` are populated on every run.
- **`ToSnapshotText()` always joins with `"\n"`**, never `Environment.NewLine`, so a snapshot compares equal across machines. Line breaks *inside* a generated file are the generator's own contract.
- **`CompileToReference` always overrides `AssemblyName`** with its `assemblyName` parameter (two assemblies in one compilation cannot share a name).
- **`AdditionalReferences`/`AdditionalAssemblies` normalize a `default` array to empty** on `init`.
- **References come from the test host's `TRUSTED_PLATFORM_ASSEMBLIES`**, loaded once per process. If that list is absent or names nothing on disk (single-file/trimmed publish, a custom `AssemblyLoadContext`), a `GeneratorAssertionException` is thrown up front rather than letting every test fail with BCL errors.
- **What `IncrementalAssert` cannot see: retention.** Roslyn's reasons answer "did this step recompute", not "what does this value hold". A model that compares by value while keeping an `ISymbol`, `SyntaxNode`, or `Compilation` in a field equality ignores passes both assertions and still pins compilations in the driver cache. Keeping symbols and syntax out of models stays a design rule, not a tested one.

## 3. DO NOT

- **DO NOT set `AllowGeneratorExceptions = true` to make a test pass.** It exists only for a generator whose contract *is* to fail loudly, or to test this behaviour itself. Turning it on hides `CS8785`/`AD0001` from the crash check and every negative assertion starts passing for the wrong reason.
- **DO NOT expect `DiagnosticIdPrefix` to hide a crash.** `CS8785` and `AD0001` are exempt from the filter by design.
- **DO NOT reference this package from the generator project you ship.** It is a `net10.0` test-project package; the generator is a `netstandard2.0` Roslyn component.
- **DO NOT use it on a test project below `net10.0`.** The package single-targets `net10.0` deliberately.
- **DO NOT call a test framework's `Assert` and expect this harness to use it.** Every failure here is a `GeneratorAssertionException`; the harness owns only the generator-shaped assertions. Mixing your framework's assertions for the rest (`Assert.Contains` on generated text, `[Theory]` data) is fine.
- **DO NOT use `RunTwice` to prove "an unrelated edit changes nothing".** It replaces the whole source file, so every syntax-driven stage is invalidated by construction. Use `RunTwiceWithCompilationChange` and add an unrelated syntax tree. (`RunTwice` with a `null` mutation re-parses identical text and *is* the strictest caching test.)
- **DO NOT assert only on value stages.** Name `"SourceOutput"` too: value stages reporting `Unchanged` while the output stage reports `Modified` means the emitter re-runs on every keystroke anyway.
- **DO NOT pass an empty `trackingNames` array** to either `IncrementalAssert` method — it is an `ArgumentException`.
- **DO NOT trust `AssertCompilesCleanly()` to check anything without real references.** Add `AdditionalAssemblies = [typeof(MyAttribute).Assembly]` so generated calls are checked against the shipping API instead of a stub pasted into the test source.
- **DO NOT rely on `GeneratedSources` production order in a multi-file snapshot.** It is stable in practice but is not a contract; set `SortGeneratedSourcesByHintName = true`.
- **DO NOT assume `CompileToReference` honours `options.AssemblyName`.** The parameter always wins; read the name from the parameter.
- **DO NOT use a `LocatedOn` snippet that occurs more than once** — it throws. Extend it until unique, or switch to `SpanStartsWith`.
- **DO NOT omit `source` when asserting the location of a *generator* diagnostic.** Generator diagnostics rebuilt from a cache-safe location record carry no syntax tree; analyzer diagnostics do.
- **DO NOT treat this as a replacement for `Microsoft.CodeAnalysis.Testing`.** Code fixes and refactorings are out of scope; use that package for `VerifyCodeFixAsync`. Both can live in one repository.

## 4. Diagnostics

This package **defines no diagnostic ids**. Two compiler/host ids matter to it:

| ID | Meaning | Handling |
|---|---|---|
| `CS8785` | A source generator threw. Reported by the compiler as a **warning**. | Turned into a `GeneratorAssertionException` unless `AllowGeneratorExceptions` is set. Never filtered out by `DiagnosticIdPrefix`. |
| `AD0001` | An analyzer threw. Reported by the analyzer host on the compilation, with no location. | Same handling. |

## 5. Canonical snippets

### Shared options for a test project

```csharp
using SsalKit.Generators.Toolkit.Testing;

internal static class GeneratorTestSupport
{
    public static readonly GeneratorTestOptions Options = new()
    {
        DiagnosticIdPrefix = "MINE",                              // drop incidental CS**** noise
        SortGeneratedSourcesByHintName = true,                    // stable multi-file snapshots
        AdditionalAssemblies = [typeof(Mine.MarkerAttribute).Assembly],  // real runtime package
    };
}
// One-off variant: Options with { AllowUnsafe = true }
```

### Run once and prove it compiles

```csharp
[Fact]
public void EmitsAGreeterForEachMarkedType()
{
    var result = GeneratorTest.Run<GreeterGenerator>(
        """
        namespace Demo;

        [Mine.Marker("hello")]
        public sealed class Widget;
        """,
        GeneratorTestSupport.Options);

    string generated = result.AssertCompilesCleanlyAndGetSource();

    Assert.Contains("public static class WidgetGreeter", generated);
}
```

### Both halves of the caching contract

```csharp
using Microsoft.CodeAnalysis.CSharp;

[Fact]
public void AnUnrelatedEditRecomputesNothing()
{
    var (_, second) = GeneratorTest.RunTwiceWithCompilationChange<GreeterGenerator>(
        Source,
        static compilation => compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// nothing to do with the generator")),
        GeneratorTestSupport.Options);

    // Name the output stage too, not just the value stages.
    IncrementalAssert.AllCachedOrUnchanged(second, "Models", "Collected", "SourceOutput");
}

[Fact]
public void AnEditTheModelCapturesFlowsThroughToTheOutput()
{
    var (first, second) = GeneratorTest.RunTwice<GreeterGenerator>(
        Source, static source => source.Replace("\"hello\"", "\"goodbye\""),
        GeneratorTestSupport.Options);

    Assert.Contains("hello", first.GetSingleSource());
    Assert.Contains("goodbye", second.GetSingleSource());

    IncrementalAssert.SomeOutputRecomputed(second, "Models");
}
```

### Diagnostics

```csharp
[Fact]
public void AMarkerOnAStructIsRejected()
{
    const string source = """
        namespace Demo;

        [Mine.Marker("hello")]
        public struct Widget;
        """;

    var result = GeneratorTest.Run<GreeterGenerator>(source, GeneratorTestSupport.Options);

    var diagnostic = DiagnosticAssert.Single(
        result.Diagnostics, "MINE001", DiagnosticSeverity.Error, exclusive: true);

    // Generator diagnostics carry no syntax tree, so the source must be passed in.
    DiagnosticAssert.LocatedOn(diagnostic, """[Mine.Marker("hello")]""", source);

    result.AssertNoGeneratedSources();
}
```

### A second assembly, for cross-assembly rules

```csharp
var contracts = GeneratorTest.CompileToReference(
    ContractsSource, "Contracts", GeneratorTestSupport.Options);   // name comes from the parameter

var result = GeneratorTest.Run<GreeterGenerator>(
    source, GeneratorTestSupport.Options with { AdditionalReferences = [contracts] });
```
