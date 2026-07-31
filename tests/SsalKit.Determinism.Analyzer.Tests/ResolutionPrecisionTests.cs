using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// The false-positive/false-negative boundaries the catalog is defined by (design §7.4): which
/// symbol a call actually resolves to is what decides, not how it is spelled.
/// </summary>
public class ResolutionPrecisionTests
{
    [Fact]
    public async Task UserOverrideOfGetHashCode_IsNotReported()
    {
        // The call resolves to Entity.GetHashCode, not object.GetHashCode, so nothing is banned; the
        // override's own body is analyzed on its own terms (and here it is deterministic).
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Entity
            {
                public int Id { get; set; }

                public override int GetHashCode() => Id;

                public override bool Equals(object? other) => other is Entity entity && entity.Id == Id;
            }

            [Deterministic]
            public sealed class Simulation
            {
                public int Key(Entity entity) => entity.GetHashCode();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task GetHashCodeOnAValueType_ThatOverridesIt_IsNotReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public int Key(int value) => value.GetHashCode();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task InjectedTimeProvider_IsTheRecommendedFix_AndIsNotReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                private readonly TimeProvider _clock;

                public Simulation(TimeProvider clock) => _clock = clock;

                public DateTimeOffset Now() => _clock.GetUtcNow();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task AmbientTimeProviderSingleton_IsReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public DateTimeOffset Now() => TimeProvider.System.GetUtcNow();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        // Only the ambient singleton is banned -- GetUtcNow() itself never is.
        DiagnosticAssert.Single(diagnostics, "SSALD001", locatedOnSnippet: "TimeProvider.System", exclusive: true);
    }

    [Fact]
    public async Task RandomInstanceMethods_AreNotReported_OnlyTheCreationIs()
    {
        // The design draws the line at where the sequence comes from: an injected Random's Next() is
        // silent, so a caller that already made the source explicit is not nagged at every draw.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public int Roll(Random random) => random.Next(1, 7);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task SeededRandomConstructor_IsStillReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public Random Create(int seed) => new Random(seed);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD002", locatedOnSnippet: "new Random(seed)", exclusive: true);
    }

    [Fact]
    public async Task MethodGroupReference_IsReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public Func<Guid> Factory() => Guid.NewGuid;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid", exclusive: true);
    }

    [Fact]
    public async Task NameofDoesNotInvokeAnything_AndIsNotReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public string Name() => nameof(DateTime.UtcNow);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task DeterministicAlternativesFromTheEcosystem_StaySilent()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;
            using SsalKit.Randomness;

            [Deterministic]
            public sealed class Simulation
            {
                private readonly DeterministicRandom _random = new DeterministicRandom(1234UL);

                public int Roll() => _random.Next(1, 7);

                public DateTimeOffset Deadline(DateTimeOffset asOf) => asOf.AddMinutes(5);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source, AnalyzerTestSupport.WithRandomness);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }
}
