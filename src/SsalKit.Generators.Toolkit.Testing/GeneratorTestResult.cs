using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// Everything one generator run produced: the files it added, the diagnostics it reported, the
/// compilation those files were added to, and the tracked incremental steps behind them.
/// </summary>
public sealed class GeneratorTestResult
{
    internal GeneratorTestResult(
        GeneratorDriverRunResult rawResult,
        Compilation outputCompilation,
        ImmutableArray<Diagnostic> diagnostics,
        GeneratorTestOptions options)
    {
        RawResult = rawResult;
        OutputCompilation = outputCompilation;
        Diagnostics = GeneratorTest.FilterById(diagnostics, options.DiagnosticIdPrefix);

        var sources = rawResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .Select(static generated => new GeneratedSource(generated.HintName, generated.SourceText.ToString()));

        if (options.SortGeneratedSourcesByHintName)
        {
            sources = sources.OrderBy(static generated => generated.HintName, StringComparer.Ordinal);
        }

        GeneratedSources = [.. sources];
    }

    /// <summary>
    /// Every file the generator added, in production order (or by hint name when
    /// <see cref="GeneratorTestOptions.SortGeneratedSourcesByHintName"/> is set).
    /// </summary>
    public ImmutableArray<GeneratedSource> GeneratedSources { get; }

    /// <summary>
    /// The diagnostics the generator reported, filtered by
    /// <see cref="GeneratorTestOptions.DiagnosticIdPrefix"/> when one is set.
    /// </summary>
    /// <remarks>
    /// <c>CS8785</c> -- the compiler's "a source generator threw" warning -- is never filtered out,
    /// whatever the prefix is. A run can only get this far carrying one when
    /// <see cref="GeneratorTestOptions.AllowGeneratorExceptions"/> was set, and a test that opted
    /// into a crash needs to be able to assert on it.
    /// </remarks>
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The compilation with the generated sources added -- what <see cref="AssertCompilesCleanly"/>
    /// re-checks, and what a test needs to look up a generated symbol.
    /// </summary>
    public Compilation OutputCompilation { get; }

    /// <summary>
    /// The raw Roslyn run result: the escape hatch for anything this class does not wrap, including
    /// the unfiltered generator diagnostics and any exception the generator threw.
    /// </summary>
    public GeneratorDriverRunResult RawResult { get; }

    /// <summary>
    /// The generator's tracked incremental steps, keyed by the name passed to
    /// <c>WithTrackingName</c>. Tracking is always enabled, so this is populated for every run;
    /// <see cref="IncrementalAssert"/> consumes it.
    /// </summary>
    /// <remarks>
    /// This covers the <em>value</em> pipeline only. The output stages registered with
    /// <c>RegisterSourceOutput</c>/<c>RegisterImplementationSourceOutput</c> are tracked separately,
    /// under <see cref="TrackedOutputSteps"/>.
    /// </remarks>
    public ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> TrackedSteps =>
        RawResult.Results.Single().TrackedSteps;

    /// <summary>
    /// The generator's tracked <em>output</em> steps -- the ones registered with
    /// <c>RegisterSourceOutput</c> and friends -- keyed by the well-known output name
    /// (<c>"SourceOutput"</c> for <c>RegisterSourceOutput</c>).
    /// </summary>
    /// <remarks>
    /// Roslyn keeps these in a dictionary of their own rather than in <see cref="TrackedSteps"/>,
    /// and an output step carries no <c>WithTrackingName</c> of its own, so its cache state is the
    /// one part of a pipeline that is easy to never look at. It is also the part that decides
    /// whether the generator re-emits a file: a value stage that says <c>Unchanged</c> while the
    /// output stage still says <c>Modified</c> means the emitter is being re-run (and the
    /// compilation re-parsed) on every keystroke after all.
    /// <see cref="IncrementalAssert"/> resolves a requested name against this dictionary as well as
    /// <see cref="TrackedSteps"/>, so <c>"SourceOutput"</c> can be asserted on by name.
    /// </remarks>
    public ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> TrackedOutputSteps =>
        RawResult.Results.Single().TrackedOutputSteps;

    /// <summary>
    /// The compiler errors, if any, in the compilation <em>after</em> the generated sources were
    /// added -- which is what proves the emitted code does not merely look right but type-checks
    /// against the API it calls into.
    /// </summary>
    /// <returns>The error-severity diagnostics of <see cref="OutputCompilation"/>.</returns>
    public ImmutableArray<Diagnostic> GetCompilationErrors() =>
        [.. OutputCompilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

    /// <summary>
    /// The text of the one file the generator produced.
    /// </summary>
    /// <returns>The single generated source's text.</returns>
    /// <exception cref="GeneratorAssertionException">The run produced a number of files other than
    /// one; the message lists the hint names it did produce.</exception>
    public string GetSingleSource()
    {
        if (GeneratedSources.Length != 1)
        {
            throw new GeneratorAssertionException(
                $"Expected the run to produce exactly one generated source, but it produced " +
                $"{GeneratedSources.Length}.{DescribeGeneratedSources()}");
        }

        return GeneratedSources[0].Text;
    }

    /// <summary>
    /// The text of the one generated file whose hint name ends with
    /// <paramref name="hintNameSuffix"/>.
    /// </summary>
    /// <param name="hintNameSuffix">A suffix of the wanted file's hint name -- the full hint name
    /// for an exact lookup, or something like <c>"ServiceCollectionExtensions.g.cs"</c> when the
    /// leading part of the name varies with the assembly or type under test.</param>
    /// <returns>The matching generated source's text.</returns>
    /// <exception cref="GeneratorAssertionException">No generated file, or more than one, has that
    /// suffix; the message lists every hint name produced.</exception>
    public string GetSource(string hintNameSuffix)
    {
        ArgumentException.ThrowIfNullOrEmpty(hintNameSuffix);

        var matches = GeneratedSources
            .Where(generated => generated.HintName.EndsWith(hintNameSuffix, StringComparison.Ordinal))
            .ToImmutableArray();

        if (matches.Length != 1)
        {
            throw new GeneratorAssertionException(
                $"Expected exactly one generated source whose hint name ends with '{hintNameSuffix}', " +
                $"but found {matches.Length}.{DescribeGeneratedSources()}");
        }

        return matches[0].Text;
    }

    /// <summary>
    /// Every generated file in one string, each preceded by a <c>// ==== &lt;hint name&gt;</c> header
    /// line -- the text to hand to a snapshot/approval library when a single snapshot should cover
    /// the whole run rather than one file of it.
    /// </summary>
    /// <returns>The generated sources concatenated in the order of
    /// <see cref="GeneratedSources"/>, or an empty string when the run produced nothing.</returns>
    /// <remarks>
    /// <para>
    /// Because the hint names are part of the text, the snapshot also records which files were
    /// produced and under what names -- the half of a generator's output that per-file snapshots
    /// silently omit. Pair it with
    /// <see cref="GeneratorTestOptions.SortGeneratedSourcesByHintName"/> so the snapshot cannot
    /// churn when an unrelated edit reorders the generator's output.
    /// </para>
    /// <para>
    /// The header lines and the joins between files are always <c>"\n"</c>, never
    /// <see cref="Environment.NewLine"/>: a snapshot is a file checked in and compared across
    /// machines, so a separator that differs between Windows and Linux would make the same
    /// generator output mismatch depending on where the test ran. (Line breaks <em>inside</em> a
    /// generated file are whatever the generator itself emitted; that is the generator's contract,
    /// not this method's.)
    /// </para>
    /// </remarks>
    public string ToSnapshotText() =>
        string.Join(
            "\n",
            GeneratedSources.Select(static generated => "// ==== " + generated.HintName + "\n" + generated.Text));

    /// <summary>
    /// Asserts that the compilation still has no errors once the generated sources are in it.
    /// </summary>
    /// <returns>This result, so the assertion can be chained before reading a source.</returns>
    /// <exception cref="GeneratorAssertionException">The regenerated compilation has errors; the
    /// message lists them.</exception>
    public GeneratorTestResult AssertCompilesCleanly()
    {
        var errors = GetCompilationErrors();

        if (!errors.IsEmpty)
        {
            throw new GeneratorAssertionException(
                $"Generated code failed to compile ({errors.Length} error(s)):{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors.Select(static error => "  " + error.ToString())) +
                DescribeGeneratedSources());
        }

        return this;
    }

    /// <summary>
    /// Asserts that the compilation still has no errors once the generated sources are in it, then
    /// returns the text of the one file the generator produced.
    /// </summary>
    /// <returns>The single generated source's text.</returns>
    /// <exception cref="GeneratorAssertionException">The regenerated compilation has errors, or the
    /// run produced a number of files other than one.</exception>
    /// <remarks>
    /// Exactly <see cref="AssertCompilesCleanly"/> followed by <see cref="GetSingleSource"/> -- the
    /// pair almost every test of a single-output generator opens with, so it is worth one name.
    /// </remarks>
    public string AssertCompilesCleanlyAndGetSource() => AssertCompilesCleanly().GetSingleSource();

    /// <summary>
    /// Asserts that the generator produced no files at all -- the assertion for the "this input is
    /// deliberately ignored" half of a generator's contract.
    /// </summary>
    /// <returns>This result, so the assertion can be chained.</returns>
    /// <exception cref="GeneratorAssertionException">The run produced at least one file; the
    /// message lists their hint names.</exception>
    public GeneratorTestResult AssertNoGeneratedSources()
    {
        if (!GeneratedSources.IsEmpty)
        {
            throw new GeneratorAssertionException(
                $"Expected the run to produce no generated sources, but it produced " +
                $"{GeneratedSources.Length}.{DescribeGeneratedSources()}");
        }

        return this;
    }

    private string DescribeGeneratedSources()
    {
        if (GeneratedSources.IsEmpty)
        {
            return Environment.NewLine + "Generated sources: (none)";
        }

        return Environment.NewLine + "Generated sources:" + Environment.NewLine +
            string.Join(
                Environment.NewLine,
                GeneratedSources.Select(static generated => "  - " + generated.HintName));
    }
}
