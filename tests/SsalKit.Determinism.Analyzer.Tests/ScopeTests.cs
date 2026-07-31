using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// The scope matrix (design §5.1/§7.2): where a <c>[Deterministic]</c> marking reaches, and where it
/// deliberately does not.
/// </summary>
public class ScopeTests
{
    [Fact]
    public async Task Method_Marked_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic]
                public Guid Next() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task Constructor_Marked_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                private readonly Guid _id;

                [Deterministic]
                public Simulation() => _id = Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task PropertyAccessor_MarkedOnTheProperty_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic]
                public DateTime StartedAt
                {
                    get { return DateTime.UtcNow; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD001", locatedOnSnippet: "DateTime.UtcNow", exclusive: true);
    }

    [Fact]
    public async Task Class_Marked_CoversEveryMember()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public Guid First() => Guid.NewGuid();

                public DateTime Second() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()");
        DiagnosticAssert.Single(diagnostics, "SSALD001", locatedOnSnippet: "DateTime.UtcNow");
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public async Task Struct_Marked_CoversEveryMember()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public readonly struct Tick
            {
                public Guid Next() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task NestedType_InsideAMarkedType_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public sealed class State
                {
                    public Guid Next() => Guid.NewGuid();
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task Lambda_InsideAMarkedMember_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic]
                public Func<Guid> Factory()
                {
                    return () => Guid.NewGuid();
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task LocalFunction_InsideAMarkedMember_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic]
                public Guid Run()
                {
                    return Local();

                    static Guid Local() => Guid.NewGuid();
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task FieldInitializer_InAMarkedType_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                private readonly Guid _id = Guid.NewGuid();

                public Guid Id => _id;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task PropertyInitializer_InAMarkedType_IsInScope()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                public Guid Id { get; } = Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task UnmarkedCode_IsSilent()
    {
        const string Source = """
            using System;

            public sealed class Ordinary
            {
                public Guid Next() => Guid.NewGuid();

                public DateTime Now() => DateTime.UtcNow;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task BaseTypeMarking_DoesNotPropagateToDerivedTypes()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public abstract class SimulationBase
            {
            }

            public sealed class Derived : SimulationBase
            {
                public Guid Next() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task InterfaceMarkingIsImpossible_SoImplementationsAreNotInScope()
    {
        // [Deterministic] has no Interface target, so an interface cannot carry it at all; this pins
        // the consequence -- an implementation is in scope only when it says so itself.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public interface ISimulation
            {
                Guid Next();
            }

            public sealed class Implementation : ISimulation
            {
                public Guid Next() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task PartialType_MarkedOnOnePart_CoversTheOther()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed partial class Simulation
            {
            }

            public sealed partial class Simulation
            {
                public Guid Next() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task SiblingMembers_OutsideTheMarkedOne_StaySilent()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic]
                public Guid Marked() => Guid.NewGuid();

                public Guid Unmarked() => Guid.CreateVersion7();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", DiagnosticSeverity.Warning, "Guid.NewGuid()", exclusive: true);
    }
}
