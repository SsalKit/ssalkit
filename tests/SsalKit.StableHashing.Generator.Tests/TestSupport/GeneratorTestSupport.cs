using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.StableHashing.Generator.Tests.TestSupport;

/// <summary>
/// The few facts about <em>this</em> package that every test in this project shares: which
/// assembly the generated code is type-checked against, which diagnostic prefix belongs to it, and
/// which generator is under test. Everything else comes from <see cref="GeneratorTest"/>.
/// </summary>
internal static class GeneratorTestSupport
{
    /// <summary>
    /// The default options: the real <see cref="StableHashContractAttribute"/>,
    /// <see cref="StableHashMemberAttribute"/>, <c>StableHashWriter</c>, and <c>StableHash64</c>, so
    /// the generated code is type-checked against the shipping API. Only <c>SSALH</c> diagnostics
    /// are surfaced, so a deliberately invalid test source's incidental compiler diagnostics never
    /// reach an assertion.
    /// </summary>
    public static readonly GeneratorTestOptions Options = new()
    {
        DiagnosticIdPrefix = "SSALH",
        AdditionalAssemblies = [typeof(StableHashContractAttribute).Assembly],
        SortGeneratedSourcesByHintName = true,
    };

    /// <summary>
    /// Runs the real <see cref="StableHashGenerator"/> over <paramref name="source"/>.
    /// </summary>
    public static GeneratorTestResult RunGenerator(string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.Run<StableHashGenerator>(source, options ?? Options);
}
