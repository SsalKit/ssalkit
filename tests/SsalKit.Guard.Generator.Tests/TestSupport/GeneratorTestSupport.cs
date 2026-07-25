using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Guard.Generator.Tests.TestSupport;

/// <summary>
/// The few facts about <em>this</em> package that every test in this project shares: which
/// assembly the generated code is type-checked against, which diagnostic prefix belongs to it, and
/// which generator is under test. Everything else comes from <see cref="GeneratorTest"/>.
/// </summary>
internal static class GeneratorTestSupport
{
    /// <summary>
    /// The default options: the real <see cref="SsalKit.Guard.ErrorCodedException"/>,
    /// <c>[ErrorCode&lt;TCode&gt;]</c>, <c>[ErrorCodes&lt;TCode&gt;]</c> and
    /// <c>[ExternalErrorCode&lt;TCode&gt;]</c>, so the generated code is type-checked against the
    /// shipping API. Only <c>SSALG</c> diagnostics are surfaced, so a deliberately invalid test
    /// source's incidental compiler diagnostics never reach an assertion, and the generated files
    /// are ordered by hint name so a multi-file snapshot cannot churn on production order.
    /// </summary>
    public static readonly GeneratorTestOptions Options = new()
    {
        DiagnosticIdPrefix = "SSALG",
        SortGeneratedSourcesByHintName = true,
        AdditionalAssemblies = [typeof(SsalKit.Guard.ErrorCodedException).Assembly],
    };

    /// <summary>
    /// Runs the real <see cref="ErrorCodesGenerator"/> over <paramref name="source"/>.
    /// </summary>
    public static GeneratorTestResult RunGenerator(string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.Run<ErrorCodesGenerator>(source, options ?? Options);

    /// <summary>
    /// Every generated file with its hint name above it, for the multi-container snapshots, whose
    /// point is to cover the file names as well as their contents.
    /// </summary>
    public static string AllSourcesWithHintNames(this GeneratorTestResult result) =>
        string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(generated =>
                "// ==== " + generated.HintName + Environment.NewLine + generated.Text));
}
