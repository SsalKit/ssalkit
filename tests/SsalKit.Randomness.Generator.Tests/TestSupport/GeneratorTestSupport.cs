using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Randomness.Generator.Tests.TestSupport;

/// <summary>
/// The few facts about <em>this</em> package that every test in this project shares: which
/// assembly the generated code is type-checked against, which diagnostic prefix belongs to it, and
/// which generator is under test. Everything else comes from <see cref="GeneratorTest"/>.
/// </summary>
internal static class GeneratorTestSupport
{
    /// <summary>
    /// The default options: the real <see cref="RandomWeightAttribute"/>, <c>IRandomSource</c>,
    /// <c>WeightedRandomExtensions</c> and <c>WeightedSampler&lt;T&gt;</c>, so the generated code is
    /// type-checked against the shipping API. Only <c>SSALR</c> diagnostics are surfaced, so a
    /// deliberately invalid test source's incidental compiler diagnostics never reach an assertion.
    /// </summary>
    public static readonly GeneratorTestOptions Options = new()
    {
        DiagnosticIdPrefix = "SSALR",
        AdditionalAssemblies = [typeof(RandomWeightAttribute).Assembly],
    };

    /// <summary>
    /// Runs the real <see cref="RandomWeightGenerator"/> over <paramref name="source"/>.
    /// </summary>
    public static GeneratorTestResult RunGenerator(string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.Run<RandomWeightGenerator>(source, options ?? Options);
}
