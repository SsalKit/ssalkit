using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing;
using SsalKit.StableHashing.Generator.Tests.TestSupport;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// Verifies that <see cref="StableHashGenerator"/>'s pipeline actually caches: an unrelated
/// compilation change must not force the collected/analysed stages to recompute, which relies on
/// every model that flows through the pipeline being value-equal across runs (records over
/// primitives, <c>EquatableArray&lt;T&gt;</c> for collections, and <c>LocationInfo</c> instead of a
/// <see cref="Location"/> that would pin a syntax tree).
/// </summary>
public class GeneratorIncrementalTests
{
    private const string ValidSource = """
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        [StableHashContract("game.player-snapshot", Version = 1)]
        public sealed class PlayerSnapshot
        {
            [StableHashMember(1)] public int Score { get; init; }
        }
        """;

    private const string DiagnosticSource = """
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        [StableHashContract("game.player-snapshot", Version = 1)]
        public class PlayerSnapshot
        {
            [StableHashMember(1)] public int Score { get; init; }
        }
        """;

    private const string ValidSourceWithEditedBody = """
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        [StableHashContract("game.player-snapshot", Version = 1)]
        public sealed class PlayerSnapshot
        {
            [StableHashMember(1)] public int Score { get; init; }

            public int Doubled()
            {
                // Body added; nothing the model captures changed, so every downstream stage must
                // still be able to reuse its previous output.
                return Score * 2;
            }
        }
        """;

    private const string ValidSourceWithExtraMember = """
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        [StableHashContract("game.player-snapshot", Version = 1)]
        public sealed class PlayerSnapshot
        {
            [StableHashMember(1)] public int Score { get; init; }
            [StableHashMember(2)] public int Level { get; init; }
        }
        """;

    [Fact]
    public void UnrelatedSyntaxTreeAddition_ReusesEveryCollectedStage()
    {
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(ValidSource);

        AssertEveryCollectedStageReused(second);
    }

    [Fact]
    public void DiagnosticProducingSource_UnrelatedChange_ReusesEveryCollectedStage()
    {
        // A DiagnosticInfo carries a LocationInfo, not a Location: if it held the real thing, the
        // pipeline would compare two runs' diagnostics by reference and never cache.
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(DiagnosticSource);

        AssertEveryCollectedStageReused(second);
    }

    [Fact]
    public void UnrelatedMethodAddedToTheDecoratedType_ReusesTheCollectedAndAnalysedStages()
    {
        // The per-type transform necessarily re-runs (the target's syntax tree changed), but the
        // model it produces is identical, so nothing downstream may recompute.
        var (_, second) = GeneratorTest.RunTwice<StableHashGenerator>(
            ValidSource, _ => ValidSourceWithEditedBody, GeneratorTestSupport.Options);

        AssertEveryCollectedStageReused(second);
    }

    /// <summary>
    /// The other side of the caching contract: a change the model <em>does</em> capture has to
    /// invalidate it. Adding a member changes the emitted file, so the emission stages must
    /// recompute.
    /// </summary>
    [Fact]
    public void AddingAMember_InvalidatesTheEmissionModel()
    {
        var (_, second) = GeneratorTest.RunTwice<StableHashGenerator>(
            ValidSource, _ => ValidSourceWithExtraMember, GeneratorTestSupport.Options);

        IncrementalAssert.SomeOutputRecomputed(second, StableHashGenerator.TrackingNames.Types);
    }

    private static (GeneratorTestResult First, GeneratorTestResult Second) RunWithUnrelatedSyntaxTreeAdded(
        string source) =>
        GeneratorTest.RunTwiceWithCompilationChange<StableHashGenerator>(
            source,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))),
            GeneratorTestSupport.Options);

    private static void AssertEveryCollectedStageReused(GeneratorTestResult secondRun) =>
        IncrementalAssert.AllCachedOrUnchanged(
            secondRun,
            StableHashGenerator.TrackingNames.Collected,
            StableHashGenerator.TrackingNames.Analysis,
            StableHashGenerator.TrackingNames.Types,
            StableHashGenerator.TrackingNames.Diagnostics);
}
