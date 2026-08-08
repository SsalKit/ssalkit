using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// Strict mode (design §12): inside <c>[Deterministic(Strict = true)]</c>, a direct reference to a
/// member of the same assembly that no <c>[Deterministic]</c> marking covers is SSALD008.
/// </summary>
/// <remarks>
/// The rule asks whether the callee is <em>covered</em> by a <c>[Deterministic]</c> marking -- the
/// same question SSALD007 asks, put to the callee -- not whether it is deterministic, and it reports
/// only what a consumer could actually mark. Exemptions therefore live in one of two places: nested
/// inside a <c>[Deterministic]</c> type that does cover the callee, or on the calling member. Those
/// sentences are what most of the cases below exist to pin down.
/// </remarks>
public class StrictScopeTests
{
    // ---------------------------------------------------------------------------------------------
    // 1. Positives: one per operation kind the analyzer registers.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task MethodCall_IntoAnUnmarkedType_IsReported()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => DamageTable.Lookup(roll);
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        var diagnostic = DiagnosticAssert.Single(
            diagnostics,
            "SSALD008",
            DiagnosticSeverity.Warning,
            locatedOnSnippet: "DamageTable.Lookup(roll)",
            exclusive: true);

        var message = diagnostic.GetMessage();

        // {0} names the member that was called, {1} the type an attribute would go on.
        Assert.Contains("'DamageTable.Lookup'", message, StringComparison.Ordinal);
        Assert.Contains("Mark 'DamageTable'", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConstructorCall_OnAnUnmarkedType_IsReported()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public object Create() => new DamageTable(3);
            }

            public sealed class DamageTable
            {
                public DamageTable(int scale) => Scale = scale;

                public int Scale { get; }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        var diagnostic = DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "new DamageTable(3)", exclusive: true);

        Assert.Contains("'new DamageTable'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropertyRead_OnAnUnmarkedType_IsReported()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => roll + DamageTable.Bonus;
            }

            public static class DamageTable
            {
                public static int Bonus => 10;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        var diagnostic = DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Bonus;", exclusive: true);

        Assert.Contains("'DamageTable.Bonus'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtensionMethodCall_IntoAnUnmarkedType_IsReported()
    {
        // A `value.Ext()` call binds to the reduced form of the extension method, whose
        // OriginalDefinition is the static declaration an attribute would go on. This locks that
        // the whole report path -- coverage walk, generated-code test, body test -- resolves
        // through the reduced symbol rather than tripping over it.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => roll.Doubled();
            }

            public static class RollExtensions
            {
                public static int Doubled(this int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        var diagnostic = DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "roll.Doubled()", exclusive: true);

        Assert.Contains("'RollExtensions.Doubled'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Mark 'RollExtensions'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtensionMethodCall_IntoAMarkedType_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => roll.Doubled();
            }

            [Deterministic]
            public static class RollExtensions
            {
                public static int Doubled(this int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task MethodGroupReference_ToAnUnmarkedType_IsReported()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public Func<int, int> Factory() => DamageTable.Lookup;
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup;", exclusive: true);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Strict semantics: opt-in, and part of the scope, so nearest-wins applies to it too.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task PlainDeterministic_WithoutStrict_IsSilent()
    {
        var diagnostics = await AnalyzerTestSupport.RunAsync(CallIntoAnUnmarkedHelper("[Deterministic]"));

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task ExplicitStrictFalse_IsSilent()
    {
        var diagnostics = await AnalyzerTestSupport.RunAsync(CallIntoAnUnmarkedHelper("[Deterministic(Strict = false)]"));

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task StrictOnAMember_CoversThatMemberAlone()
    {
        const string Source = """
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic(Strict = true)]
                public int Strict(int roll) => DamageTable.Lookup(roll);

                public int Unmarked(int roll) => DamageTable.Lookup(roll + 1);
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup(roll)", exclusive: true);
    }

    [Fact]
    public async Task NestedDeterministic_WithoutStrict_TurnsStrictOffInsideIt()
    {
        // Nearest-wins, without exception: Strict is a property of the marking that won, so a nearer
        // [Deterministic] that does not ask for it relaxes the nested scope on purpose.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Outer(int roll) => DamageTable.Lookup(roll);

                [Deterministic]
                public sealed class Relaxed
                {
                    public int Inner(int roll) => DamageTable.Lookup(roll + 1);
                }
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup(roll)", exclusive: true);
    }

    [Fact]
    public async Task StrictOnAType_ReachesItsNestedTypes()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public sealed class State
                {
                    public int Apply(int roll) => DamageTable.Lookup(roll);
                }
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup(roll)", exclusive: true);
    }

    [Fact]
    public async Task StrictReachesALambda()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public Func<int, int> Factory() => roll => DamageTable.Lookup(roll);
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup(roll)", exclusive: true);
    }

    [Fact]
    public async Task StrictReachesALocalFunction()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Run()
                {
                    return Local();

                    static int Local() => DamageTable.Lookup(1);
                }
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        // Local() itself stays silent: a local function's chain runs through the marked member.
        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup(1)", exclusive: true);
    }

    [Fact]
    public async Task AllowNonDeterminism_InsideAStrictScope_SilencesSsald008Too()
    {
        // The exemption carves out the whole scope, not just the catalog half of it.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                [AllowNonDeterminism(Justification = "diagnostics only; never feeds simulation state")]
                public int Report(int roll) => DamageTable.Lookup(roll);
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task BothAttributesOnOneStrictScope_ExemptionWins()
    {
        // The contradictory pair resolves to silence, and Strict does not survive it either: the
        // quieter reading of a marking nobody can interpret cannot produce a false positive.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            public sealed class Simulation
            {
                [Deterministic(Strict = true)]
                [AllowNonDeterminism]
                public int Apply(int roll) => DamageTable.Lookup(roll) + DateTime.UtcNow.Second;
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. A callee the contract covers is silent; an exemption anchored under that contract is too.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Callee_MarkedDeterministic_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => DamageTable.Lookup(roll);
            }

            public static class DamageTable
            {
                [Deterministic]
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task Callee_ExemptedInsideADeterministicType_IsSilent()
    {
        // The recommended shape, and the one the rule is built around: the contract covers the
        // helper type, and the member that genuinely needs the clock is carved back out inside it.
        // That silences SSALD008 (the chain has a [Deterministic]) and stays clear of SSALD007 (the
        // exemption is not an orphan) at the same time.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public string Describe() => Logging.Timestamp();
            }

            [Deterministic]
            public static class Logging
            {
                [AllowNonDeterminism(Justification = "wall-clock logging only")]
                public static string Timestamp() => DateTime.UtcNow.ToString("O");
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task Callee_WithAnOrphanExemption_ReportsBothTheOrphanAndTheCall()
    {
        // An exemption with no [Deterministic] above it suppresses nothing -- that is precisely what
        // SSALD007 says about it -- so it cannot be what silences SSALD008 either. Both rules run
        // off the same coverage question and therefore point the same direction: this helper is
        // outside every contract, and writing [AllowNonDeterminism] on it did not change that.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public string Describe() => Logging.Timestamp();
            }

            public static class Logging
            {
                [AllowNonDeterminism(Justification = "wall-clock logging only")]
                public static string Timestamp() => DateTime.UtcNow.ToString("O");
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics,
            "SSALD007",
            locatedOnSnippet: "AllowNonDeterminism(Justification = \"wall-clock logging only\")");
        DiagnosticAssert.Single(diagnostics, "SSALD008", locatedOnSnippet: "Logging.Timestamp()");
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public async Task CalleeContainingType_MarkedDeterministic_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => DamageTable.Lookup(roll);
            }

            [Deterministic]
            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task CalleeContainingType_ExemptedInsideADeterministicType_IsSilent()
    {
        // The same anchoring one level up: the exempt type is nested in a [Deterministic] one, so
        // the chain still reaches a marking and the exemption is still not an orphan.
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public string Describe() => Host.Logging.Timestamp();
            }

            [Deterministic]
            public static class Host
            {
                [AllowNonDeterminism]
                public static class Logging
                {
                    public static string Timestamp() => DateTime.UtcNow.ToString("O");
                }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. P1 -- what cannot be marked is not reported.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task BclCall_IsSilent()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Clamp(int value) => Math.Max(0, Math.Min(100, value));
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task CallIntoAnotherSsalKitPackage_IsSilent()
    {
        // Nothing in SsalKit.Randomness carries [Deterministic] -- those packages do not reference
        // this one, and never will -- so reporting another package's members would be unfixable.
        const string Source = """
            using SsalKit.Determinism;
            using SsalKit.Randomness;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                private readonly DeterministicRandom _random = new(20260808UL);

                public int Roll() => _random.Next(1, 7);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source, AnalyzerTestSupport.WithRandomness);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task InterfaceMemberCall_IsSilent()
    {
        // [Deterministic] has no Interface target, so an interface member could never be marked and
        // reporting one would be a permanent false positive.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                private readonly IDamageTable _table;

                public Simulation(IDamageTable table) => _table = table;

                public int Apply(int roll) => _table.Lookup(roll) + _table.Bonus;
            }

            public interface IDamageTable
            {
                int Bonus { get; }

                int Lookup(int roll);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task PositionalRecord_SynthesizedMembers_AreSilent()
    {
        // The exclusion that carries the noise budget. Nothing about a positional record was written
        // by hand: its Equals and Deconstruct are implicitly declared, and its properties and
        // primary constructor -- which Roslyn does report as explicitly declared, pointing at the
        // record header -- still have no body behind them. In code that uses records to carry data,
        // reporting any of these would drown out every real finding.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Describe(Damage damage)
                {
                    var other = new Damage(1, 2);
                    var (amount, kind) = damage;

                    return damage.Amount + amount + kind + (damage.Equals(other) ? 1 : 0);
                }
            }

            public sealed record Damage(int Amount, int Kind);
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task ImplicitParameterlessConstructor_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public object Create() => new Marker();
            }

            public sealed class Marker
            {
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task GeneratedCallee_IsSilent()
    {
        // A source generator's output is in this assembly and still out of reach: there is no file
        // to add an attribute to. Generated helper types (a generated extension class, a generated
        // registration table) are exactly the kind of thing a deterministic core calls, so without
        // this the only fix on offer would be "stop using the generator".
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => DamageTable.Lookup(roll);
            }

            [System.CodeDom.Compiler.GeneratedCode("TestGenerator", "1.0")]
            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task GeneratedCodeAttributeOnTheMember_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => DamageTable.Lookup(roll);
            }

            public static class DamageTable
            {
                [System.CodeDom.Compiler.GeneratedCode("TestGenerator", "1.0")]
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task GeneratedCodeAttributeOnTheCallerOnly_StillReports()
    {
        // The caller being generated changes nothing: generated code inside a marked scope is
        // analyzed exactly like hand-written code, and the callee here is still unmarked.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            [System.CodeDom.Compiler.GeneratedCode("TestGenerator", "1.0")]
            public sealed class Simulation
            {
                public int Apply(int roll) => DamageTable.Lookup(roll);
            }

            public static class DamageTable
            {
                public static int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "DamageTable.Lookup(roll)", exclusive: true);
    }

    [Fact]
    public async Task DelegateInvoke_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(Adjust adjust, int roll) => adjust(roll);
            }

            public delegate int Adjust(int roll);
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    // ---------------------------------------------------------------------------------------------
    // 5. P2 -- a declaration with no body of its own has nothing a marking would bring into view.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AutoImplementedProperty_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(Settings settings, int roll) => roll * settings.Multiplier;
            }

            public sealed class Settings
            {
                public int Multiplier { get; init; }
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task AbstractMethod_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(DamageTable table, int roll) => table.Lookup(roll);
            }

            public abstract class DamageTable
            {
                public abstract int Lookup(int roll);
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task PartialMethod_IsReportedThroughItsImplementingPart()
    {
        // The call binds to the defining declaration, which has no body of its own; the body lives
        // in the implementing part, and that is where the "is there anything to analyze?" test has
        // to look.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(Hooks hooks, int roll) => hooks.Lookup(roll);
            }

            public partial class Hooks
            {
                public partial int Lookup(int roll);
            }

            public partial class Hooks
            {
                public partial int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(
            diagnostics, "SSALD008", locatedOnSnippet: "hooks.Lookup(roll)", exclusive: true);
    }

    [Fact]
    public async Task PartialMethod_MarkedOnEitherPart_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(Hooks hooks, int roll) => hooks.Lookup(roll);
            }

            public partial class Hooks
            {
                public partial int Lookup(int roll);
            }

            public partial class Hooks
            {
                [Deterministic]
                public partial int Lookup(int roll) => roll * 2;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Wiring: the catalog still wins, and the existing exclusions still apply.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task CatalogHit_InAStrictScope_ReportsOnlyTheCategoryDiagnostic()
    {
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public Guid Next() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }

    [Fact]
    public async Task NameOf_InAStrictScope_IsSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public string Name() => nameof(DamageTable.Bonus);
            }

            public static class DamageTable
            {
                public static int Bonus => 10;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task FieldRead_IsSilent()
    {
        // Fields are not a registered operation kind, and [Deterministic] has no Field target
        // either, so there is nothing to report and nowhere to fix it.
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Apply(int roll) => roll + DamageTable.Bonus;
            }

            public static class DamageTable
            {
                public static readonly int Bonus = 10;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task RecursionAndSameTypeCalls_AreSilent()
    {
        const string Source = """
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                public int Run(int depth) => depth <= 0 ? Seed : Run(depth - 1);

                private static int Seed => 7;
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Hygiene: a strict scope over a lot of shapes at once, none of which is reportable.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task LargeStrictScope_OverNonReportableShapes_StaysSilentAndDoesNotCrash()
    {
        const string Source = """
            #pragma warning disable CS0219
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using SsalKit.Determinism;

            [Deterministic(Strict = true)]
            public sealed class Simulation
            {
                private static readonly int[] Table = [1, 2, 3];

                private readonly IDamageTable _external;
                private readonly Adjust _adjust;

                public Simulation(IDamageTable external, Adjust adjust)
                {
                    _external = external;
                    _adjust = adjust;
                }

                public int Multiplier { get; init; }

                public string Run(Settings settings, Damage damage, AbstractTable abstractTable)
                {
                    var (amount, kind) = damage;
                    var marker = new Marker();
                    var sum = Table.Sum();
                    var mapped = new List<int>(Table.Select(value => Math.Max(0, value))).Count;
                    var viaInterface = _external.Lookup(amount) + _external.Bonus;
                    var viaDelegate = _adjust(kind);
                    var viaAbstract = abstractTable.Lookup(amount);
                    var viaAuto = settings.Multiplier + Multiplier;
                    var viaMarked = Marked.Lookup(amount);
                    var viaExempt = Exempt.Lookup(amount);
                    var viaSelf = Local(sum);

                    return $"{marker}{mapped}{viaInterface}{viaDelegate}{viaAbstract}{viaAuto}{viaMarked}{viaExempt}{viaSelf}{damage.Equals(damage)}";

                    static int Local(int value) => value + 1;
                }
            }

            [Deterministic]
            public static class Marked
            {
                public static int Lookup(int roll) => roll * 2;
            }

            [Deterministic]
            public static class Exempt
            {
                [AllowNonDeterminism(Justification = "deliberately outside the contract")]
                public static int Lookup(int roll) => roll * 3;
            }

            public interface IDamageTable
            {
                int Bonus { get; }

                int Lookup(int roll);
            }

            public abstract class AbstractTable
            {
                public abstract int Lookup(int roll);
            }

            public sealed class Settings
            {
                public int Multiplier { get; init; }
            }

            public sealed class Marker
            {
            }

            public sealed record Damage(int Amount, int Kind);

            public delegate int Adjust(int roll);
            """;

        var diagnostics = await AnalyzerTestSupport.RunAllAsync(Source);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    /// <summary>
    /// The one shape every "is strict on?" test shares, parameterized by how the scope is declared.
    /// </summary>
    private static string CallIntoAnUnmarkedHelper(string marking) =>
        $$"""
        using SsalKit.Determinism;

        {{marking}}
        public sealed class Simulation
        {
            public int Apply(int roll) => DamageTable.Lookup(roll);
        }

        public static class DamageTable
        {
            public static int Lookup(int roll) => roll * 2;
        }
        """;
}
