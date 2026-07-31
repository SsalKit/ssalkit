using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests.TestSupport;

/// <summary>
/// The few facts about <em>this</em> package that every test in this project shares: which
/// assemblies the compilation under test references, which diagnostic prefix belongs to it, and how
/// a probe source is assembled. Everything else comes from <see cref="GeneratorTest"/>.
/// </summary>
internal static class AnalyzerTestSupport
{
    /// <summary>
    /// The diagnostic prefix owned by this package. Passing it as the filter keeps a probe source's
    /// incidental compiler diagnostics out of the assertions while still letting an
    /// <c>AD0001</c> ("an analyzer threw") through, which the harness turns into a failed assertion.
    /// </summary>
    public const string Prefix = "SSALD";

    /// <summary>
    /// The default options: the real <c>[Deterministic]</c>/<c>[AllowNonDeterminism]</c> attributes
    /// and nothing else. <b>SsalKit.Randomness is deliberately absent</b>, so these options also
    /// exercise the catalog's "type not referenced -> entry skipped" path on every single run.
    /// </summary>
    public static readonly GeneratorTestOptions Options = new()
    {
        DiagnosticIdPrefix = Prefix,
        AdditionalAssemblies = [typeof(DeterministicAttribute).Assembly],
    };

    /// <summary>
    /// The options for the tests about the conditional SsalKit.Randomness catalog entries, which
    /// only resolve in a compilation that references that package.
    /// </summary>
    public static readonly GeneratorTestOptions WithRandomness = Options with
    {
        AdditionalAssemblies =
        [
            typeof(DeterministicAttribute).Assembly,
            typeof(SsalKit.Randomness.SharedRandomSource).Assembly,
        ],
    };

    /// <summary>
    /// Runs <see cref="DeterminismAnalyzer"/> over <paramref name="source"/>.
    /// </summary>
    public static Task<ImmutableArray<Diagnostic>> RunAsync(string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.RunAnalyzerAsync<DeterminismAnalyzer>(source, options ?? Options);

    /// <summary>
    /// Runs the package's analyzers as the set a consumer gets, which is what the package's own
    /// hygiene assertions should go through even while there is only one of them.
    /// </summary>
    public static Task<ImmutableArray<Diagnostic>> RunAllAsync(
        string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.RunAnalyzersAsync(source, [new DeterminismAnalyzer()], options ?? Options);

    /// <summary>
    /// Wraps a single statement in a <c>[Deterministic]</c> probe method.
    /// </summary>
    /// <remarks>
    /// The method takes the things a negative test needs to have <em>without</em> creating them --
    /// an injected <c>TimeProvider</c>, an already-obtained random generator, a boxed value, a
    /// string, a comparer, a hash accumulator -- so that a statement about one member never drags a
    /// second banned call in with it, and <c>exclusive: true</c> stays usable throughout.
    /// </remarks>
    public static string Probe(string statement) =>
        $$"""
        #pragma warning disable CS0219, CS1998, CS0168
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Linq;
        using System.Security.Cryptography;
        using System.Threading;
        using System.Threading.Tasks;
        using SsalKit.Determinism;

        public static class ProbeHost
        {
            [Deterministic]
            public static async Task RunAsync(
                TimeProvider clock,
                RandomNumberGenerator rng,
                Random random,
                object boxedObject,
                ValueType boxedValue,
                string text,
                StringComparer comparer,
                HashCode accumulator)
            {
                {{statement}}
            }
        }
        """;
}
