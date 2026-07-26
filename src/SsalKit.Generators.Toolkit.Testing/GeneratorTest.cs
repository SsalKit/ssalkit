using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// Runs incremental source generators and analyzers against in-memory source, entirely in-process
/// (no <c>dotnet build</c>, no MSBuild, no temporary project on disk).
/// </summary>
/// <remarks>
/// <para>
/// The compilation is built from one source string plus every reference assembly the test host
/// trusts, so generated code is type-checked against the real BCL -- and, through
/// <see cref="GeneratorTestOptions.AdditionalAssemblies"/>, against the real runtime package the
/// generator emits calls into.
/// </para>
/// <para>
/// Assertion failures are reported as <see cref="GeneratorAssertionException"/>, never through a
/// test framework's <c>Assert</c> class, so this package works under xunit, NUnit, MSTest, and
/// TUnit alike.
/// </para>
/// <para>
/// A generator or analyzer that <em>throws</em> is likewise a failed assertion. Roslyn catches such
/// an exception and records it (as <c>CS8785</c>, a warning, for a generator; as <c>AD0001</c> for
/// an analyzer) instead of letting it escape, which would otherwise leave the run looking like one
/// that simply had nothing to produce -- and every negative assertion about it passing for the
/// wrong reason. Every entry point here therefore refuses to return such a run unless
/// <see cref="GeneratorTestOptions.AllowGeneratorExceptions"/> is set.
/// </para>
/// </remarks>
public static class GeneratorTest
{
    /// <summary>
    /// The compiler's id for "a source generator threw", reported as a <b>warning</b> -- so nothing
    /// that looks only at errors, and no filter that keeps only a package's own prefix, would ever
    /// see it.
    /// </summary>
    internal const string GeneratorCrashDiagnosticId = "CS8785";

    /// <summary>
    /// The analyzer host's id for "an analyzer threw", reported on the compilation with no location.
    /// </summary>
    internal const string AnalyzerCrashDiagnosticId = "AD0001";

    /// <summary>
    /// Builds the compilation a generator would run against, without running one.
    /// </summary>
    /// <param name="source">The C# source of the single syntax tree in the compilation.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>.</param>
    /// <returns>The compilation under test.</returns>
    public static CSharpCompilation CreateCompilation(string source, GeneratorTestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= GeneratorTestOptions.Default;

        var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptionsFor(options));

        return CSharpCompilation.Create(
            options.AssemblyName,
            [syntaxTree],
            ReferenceLoader.Resolve(options),
            new CSharpCompilationOptions(
                options.OutputKind,
                allowUnsafe: options.AllowUnsafe,
                nullableContextOptions: options.NullableContextOptions));
    }

    /// <summary>
    /// Compiles <paramref name="source"/> into an in-memory assembly and returns a reference to it,
    /// for tests that need a second, separately compiled assembly -- cross-assembly accessibility,
    /// <c>extern alias</c>, <c>protected internal</c>, or <c>[InternalsVisibleTo]</c> rules that
    /// cannot be exercised from a single compilation.
    /// </summary>
    /// <param name="source">The C# source of the assembly to compile.</param>
    /// <param name="assemblyName">The name of the assembly to compile.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>. <see cref="GeneratorTestOptions.AssemblyName"/>
    /// is overridden by <paramref name="assemblyName"/>.</param>
    /// <returns>A metadata reference to the compiled assembly, ready to be passed through
    /// <see cref="GeneratorTestOptions.AdditionalReferences"/>.</returns>
    /// <exception cref="GeneratorAssertionException">The source failed to compile.</exception>
    public static MetadataReference CompileToReference(
        string source, string assemblyName, GeneratorTestOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        options ??= GeneratorTestOptions.Default;

        var compilation = CreateCompilation(source, options with { AssemblyName = assemblyName });

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
        {
            throw new GeneratorAssertionException(
                $"Failed to compile reference assembly '{assemblyName}':{Environment.NewLine}" +
                string.Join(
                    Environment.NewLine,
                    emitResult.Diagnostics
                        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                        .Select(static diagnostic => "  " + diagnostic.ToString())));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    /// <summary>
    /// Runs <typeparamref name="TGenerator"/> over <paramref name="source"/> once.
    /// </summary>
    /// <typeparam name="TGenerator">The incremental generator to run.</typeparam>
    /// <param name="source">The C# source the generator sees.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>.</param>
    /// <returns>The generated sources, the generator's diagnostics, and the compilation the
    /// generated sources were added to.</returns>
    public static GeneratorTestResult Run<TGenerator>(string source, GeneratorTestOptions? options = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        options ??= GeneratorTestOptions.Default;

        var driver = CreateDriver(new TGenerator(), options);
        var compilation = CreateCompilation(source, options);

        return RunOnce(driver, compilation, options).Result;
    }

    /// <summary>
    /// Runs <typeparamref name="TGenerator"/> twice on the same driver, editing the source in
    /// between, so the second run's tracked steps can be handed to <see cref="IncrementalAssert"/>.
    /// </summary>
    /// <typeparam name="TGenerator">The incremental generator to run.</typeparam>
    /// <param name="source">The C# source of the first run.</param>
    /// <param name="mutateForSecondRun">Produces the second run's source from the first run's. When
    /// <c>null</c>, the identical source is re-parsed, which is the strictest caching test there
    /// is: nothing the pipeline observes changed, so nothing may recompute.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>.</param>
    /// <returns>Both runs' results. Incremental step tracking is enabled on both.</returns>
    /// <remarks>
    /// The whole source file is replaced, so any edit -- even one the pipeline's models ignore --
    /// invalidates the syntax-driven stages. To assert that an edit somewhere *else* in the
    /// compilation changes nothing, use
    /// <see cref="RunTwiceWithCompilationChange{TGenerator}"/> and add an unrelated syntax tree.
    /// </remarks>
    public static (GeneratorTestResult First, GeneratorTestResult Second) RunTwice<TGenerator>(
        string source,
        Func<string, string>? mutateForSecondRun = null,
        GeneratorTestOptions? options = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= GeneratorTestOptions.Default;

        var secondSource = mutateForSecondRun is null ? source : mutateForSecondRun(source);
        var parseOptions = ParseOptionsFor(options);

        return RunTwiceWithCompilationChange<TGenerator>(
            source,
            compilation => compilation.ReplaceSyntaxTree(
                compilation.SyntaxTrees.First(), CSharpSyntaxTree.ParseText(secondSource, parseOptions)),
            options);
    }

    /// <summary>
    /// Runs <typeparamref name="TGenerator"/> twice on the same driver, changing the compilation in
    /// between, so the second run's tracked steps can be handed to <see cref="IncrementalAssert"/>.
    /// </summary>
    /// <typeparam name="TGenerator">The incremental generator to run.</typeparam>
    /// <param name="source">The C# source of the first run.</param>
    /// <param name="changeForSecondRun">Produces the second run's compilation from the first run's,
    /// typically by adding or replacing a syntax tree.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>.</param>
    /// <returns>Both runs' results. Incremental step tracking is enabled on both.</returns>
    public static (GeneratorTestResult First, GeneratorTestResult Second) RunTwiceWithCompilationChange<TGenerator>(
        string source,
        Func<Compilation, Compilation> changeForSecondRun,
        GeneratorTestOptions? options = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        ArgumentNullException.ThrowIfNull(changeForSecondRun);
        options ??= GeneratorTestOptions.Default;

        var driver = CreateDriver(new TGenerator(), options);
        var compilation = CreateCompilation(source, options);

        var (firstDriver, first) = RunOnce(driver, compilation, options);
        var (_, second) = RunOnce(firstDriver, changeForSecondRun(compilation), options);

        return (first, second);
    }

    /// <summary>
    /// Runs a single analyzer over <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer to run.</typeparam>
    /// <param name="source">The C# source the analyzer sees.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>.</param>
    /// <returns>The analyzer's diagnostics together with the compiler's, filtered by
    /// <see cref="GeneratorTestOptions.DiagnosticIdPrefix"/> when one is set.</returns>
    public static Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(
        string source, GeneratorTestOptions? options = null)
        where TAnalyzer : DiagnosticAnalyzer, new() =>
        RunAnalyzersAsync(source, [new TAnalyzer()], options);

    /// <summary>
    /// Runs several analyzers over <paramref name="source"/> together, exactly as they run when the
    /// package that ships them is consumed.
    /// </summary>
    /// <param name="source">The C# source the analyzers see.</param>
    /// <param name="analyzers">The analyzers to run. Running a package's analyzers as a set is what
    /// proves the others stay silent about whichever construct the test source uses.</param>
    /// <param name="options">Compilation options, or <c>null</c> for
    /// <see cref="GeneratorTestOptions.Default"/>.</param>
    /// <returns>The analyzers' diagnostics together with the compiler's, filtered by
    /// <see cref="GeneratorTestOptions.DiagnosticIdPrefix"/> when one is set.</returns>
    /// <exception cref="ArgumentException"><paramref name="analyzers"/> is empty.</exception>
    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzersAsync(
        string source, IEnumerable<DiagnosticAnalyzer> analyzers, GeneratorTestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(analyzers);
        options ??= GeneratorTestOptions.Default;

        var analyzerArray = analyzers.ToImmutableArray();

        if (analyzerArray.IsEmpty)
        {
            throw new ArgumentException("At least one analyzer must be supplied.", nameof(analyzers));
        }

        var compilation = CreateCompilation(source, options);
        var diagnostics = await compilation.WithAnalyzers(analyzerArray).GetAllDiagnosticsAsync().ConfigureAwait(false);

        if (!options.AllowGeneratorExceptions)
        {
            ThrowIfAnAnalyzerThrew(diagnostics);
        }

        return FilterById(diagnostics, options.DiagnosticIdPrefix);
    }

    /// <summary>
    /// Narrows <paramref name="diagnostics"/> to the ids starting with <paramref name="idPrefix"/>,
    /// or returns them unchanged when no prefix is configured.
    /// </summary>
    /// <remarks>
    /// <see cref="GeneratorCrashDiagnosticId"/> and <see cref="AnalyzerCrashDiagnosticId"/> survive
    /// the filter unconditionally. A prefix exists so that a deliberately invalid test source's
    /// incidental <c>CS****</c> noise does not have to be filtered out by hand -- but the one
    /// <c>CS****</c> that says "your generator crashed" is not noise, and silently dropping it is
    /// exactly how a crashed run passes a test.
    /// </remarks>
    internal static ImmutableArray<Diagnostic> FilterById(ImmutableArray<Diagnostic> diagnostics, string? idPrefix)
    {
        if (idPrefix is null)
        {
            return diagnostics;
        }

        return
        [
            .. diagnostics.Where(diagnostic =>
                diagnostic.Id.StartsWith(idPrefix, StringComparison.Ordinal) || IsCrashDiagnostic(diagnostic)),
        ];
    }

    private static bool IsCrashDiagnostic(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, GeneratorCrashDiagnosticId, StringComparison.Ordinal)
        || string.Equals(diagnostic.Id, AnalyzerCrashDiagnosticId, StringComparison.Ordinal);

    /// <summary>
    /// Turns a generator that threw into a failed assertion, instead of a run whose every negative
    /// assertion passes because the generator never got to do anything.
    /// </summary>
    private static void ThrowIfAGeneratorThrew(GeneratorDriverRunResult runResult)
    {
        foreach (var generatorResult in runResult.Results)
        {
            if (generatorResult.Exception is null)
            {
                continue;
            }

            // Type.ToString() is the fully qualified name, and unlike Type.FullName it is never
            // null -- there is no "the generator has no name" case to fall back from.
            var generatorName = generatorResult.Generator.GetGeneratorType().ToString();

            throw new GeneratorAssertionException(
                $"The generator '{generatorName}' threw {generatorResult.Exception.GetType().FullName}: " +
                $"{generatorResult.Exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Roslyn catches this and reports it as a {GeneratorCrashDiagnosticId} warning rather than " +
                "letting it escape, so the run would otherwise look like a generator that simply produced " +
                "nothing. Set " +
                $"{nameof(GeneratorTestOptions)}.{nameof(GeneratorTestOptions.AllowGeneratorExceptions)} when the " +
                $"crash is what the test is about.{Environment.NewLine}{Environment.NewLine}" +
                $"Generator stack trace:{Environment.NewLine}{generatorResult.Exception.StackTrace}");
        }
    }

    /// <summary>
    /// The analyzer-side counterpart of <see cref="ThrowIfAGeneratorThrew"/>: the host reports a
    /// thrown analyzer as <c>AD0001</c> and carries on, so nothing else would notice.
    /// </summary>
    private static void ThrowIfAnAnalyzerThrew(ImmutableArray<Diagnostic> diagnostics)
    {
        var crashes = diagnostics
            .Where(diagnostic =>
                string.Equals(diagnostic.Id, AnalyzerCrashDiagnosticId, StringComparison.Ordinal))
            .ToImmutableArray();

        if (crashes.IsEmpty)
        {
            return;
        }

        throw new GeneratorAssertionException(
            $"{crashes.Length} analyzer(s) threw. The host catches an analyzer exception and reports it as " +
            $"{AnalyzerCrashDiagnosticId} rather than letting it escape, so the run would otherwise look like " +
            "analyzers that simply had nothing to say. Set " +
            $"{nameof(GeneratorTestOptions)}.{nameof(GeneratorTestOptions.AllowGeneratorExceptions)} when the " +
            $"crash is what the test is about.{Environment.NewLine}{Environment.NewLine}" +
            string.Join(
                Environment.NewLine,
                crashes.Select(static crash => "  - " + crash.GetMessage(CultureInfo.InvariantCulture))));
    }

    private static CSharpParseOptions ParseOptionsFor(GeneratorTestOptions options) =>
        new(options.LanguageVersion);

    private static GeneratorDriver CreateDriver(IIncrementalGenerator generator, GeneratorTestOptions options) =>
        CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: ParseOptionsFor(options),
            // Tracking is always on: it costs nothing at test scale and it is what makes
            // IncrementalAssert usable without the caller having to have opted in up front.
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static (GeneratorDriver Driver, GeneratorTestResult Result) RunOnce(
        GeneratorDriver driver, Compilation compilation, GeneratorTestOptions options)
    {
        var ranDriver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        var runResult = ranDriver.GetRunResult();

        if (!options.AllowGeneratorExceptions)
        {
            ThrowIfAGeneratorThrew(runResult);
        }

        return (ranDriver, new GeneratorTestResult(runResult, outputCompilation, diagnostics, options));
    }
}
