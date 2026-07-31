using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// The opt-out semantics (design §5.1/§5.5/§7.3): <c>[AllowNonDeterminism]</c> silences the scope it
/// is on, a nearer <c>[Deterministic]</c> re-enables it, and an application with no
/// <c>[Deterministic]</c> above it at all is reported as SSALD007.
/// </summary>
public class OptOutTests
{
    [Fact]
    public async Task AllowNonDeterminism_OnAMember_SilencesIt()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [AllowNonDeterminism(Justification = "wall-clock logging only")]
                public DateTime LoggedAt() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task AllowNonDeterminism_WithoutJustification_BehavesIdentically()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [AllowNonDeterminism]
                public DateTime LoggedAt() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task AllowNonDeterminism_OnANestedType_SilencesEverythingInIt()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [AllowNonDeterminism]
                public sealed class Diagnostics
                {
                    public DateTime LoggedAt() => DateTime.UtcNow;
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task NestedDeterministic_InsideAnExemptScope_ReEnablesAnalysis()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [AllowNonDeterminism]
                public sealed class Diagnostics
                {
                    public DateTime LoggedAt() => DateTime.UtcNow;

                    [Deterministic]
                    public Guid StableId() => Guid.NewGuid();
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task OrphanAllowNonDeterminism_IsReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Ordinary
            {
                [AllowNonDeterminism(Justification = "nothing to suppress here")]
                public DateTime LoggedAt() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        var diagnostic = DiagnosticAssert.Single(
            diagnostics,
            "SSALD007",
            DiagnosticSeverity.Warning,
            locatedOnSnippet: "AllowNonDeterminism(Justification = \"nothing to suppress here\")",
            exclusive: true);

        Assert.Contains("Ordinary.LoggedAt", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrphanAllowNonDeterminism_OnAType_IsReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [AllowNonDeterminism]
            public sealed class Ordinary
            {
                public DateTime LoggedAt() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSALD007", exclusive: true);

        Assert.Contains("Ordinary", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowNonDeterminism_InsideADeterministicScope_IsNotOrphaned()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [AllowNonDeterminism]
                public DateTime LoggedAt() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, "SSALD007");
    }

    [Fact]
    public async Task AllowNonDeterminism_NestedInsideAnotherOne_IsRedundantButNotOrphaned()
    {
        // Redundant markings are deliberately not diagnosed (design §5.1); only ones with no
        // [Deterministic] anywhere above them are.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [AllowNonDeterminism]
                public sealed class Diagnostics
                {
                    [AllowNonDeterminism]
                    public DateTime LoggedAt() => DateTime.UtcNow;
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task BothAttributesOnOneSymbol_ExemptionWins_AndIsNotOrphaned()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic]
                [AllowNonDeterminism]
                public DateTime LoggedAt() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task PragmaSuppression_IsTheCallSiteAlternative()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public DateTime LoggedAt()
                {
            #pragma warning disable SSALD001
                    return DateTime.UtcNow;
            #pragma warning restore SSALD001
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }
}
