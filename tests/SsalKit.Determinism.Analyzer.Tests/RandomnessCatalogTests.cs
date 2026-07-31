using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// The conditional part of the catalog (design §5.3/§7.5): the SsalKit.Randomness entry points join
/// the ban list only in a compilation that references that package, which is the device that keeps
/// this package's dependency count at zero while still naming its own non-deterministic APIs.
/// </summary>
public class RandomnessCatalogTests
{
    [Theory]
    [InlineData("public IRandomSource Source() => SharedRandomSource.Instance;", "SharedRandomSource.Instance")]
    [InlineData("public IRandomSource Source() => CryptoRandomSource.Instance;", "CryptoRandomSource.Instance")]
    [InlineData("public DeterministicRandom Source() => DeterministicRandom.CreateRandomlySeeded();", "DeterministicRandom.CreateRandomlySeeded()")]
    public async Task RandomnessEntryPoint_WithThePackageReferenced_IsReported(string member, string snippet)
    {
        var source = $$"""
            using System;
            using SsalKit.Determinism;
            using SsalKit.Randomness;

            [Deterministic]
            public sealed class Simulation
            {
                {{member}}
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(source, AnalyzerTestSupport.WithRandomness);

        DiagnosticAssert.Single(
            diagnostics, "SSALD002", DiagnosticSeverity.Warning, locatedOnSnippet: snippet, exclusive: true);
    }

    [Fact]
    public async Task WithoutTheRandomnessReference_TheAnalyzerStillWorksAndDoesNotCrash()
    {
        // The default options reference only the SsalKit.Determinism runtime, so every Randomness
        // catalog entry hits the GetTypeByMetadataName-returned-null path. The rest of the catalog
        // has to keep working through it.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public DateTime Now() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD001", locatedOnSnippet: "DateTime.UtcNow", exclusive: true);
    }
}
