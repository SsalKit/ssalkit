using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing;
using SsalKit.Randomness.Generator.Tests.TestSupport;

namespace SsalKit.Randomness.Generator.Tests;

/// <summary>
/// Verifies that <see cref="RandomWeightGenerator"/>'s pipeline actually caches: an unrelated
/// compilation change must not force the collected/analysed stages to recompute, which relies on
/// every model that flows through the pipeline being value-equal across runs (records over
/// primitives, <c>EquatableArray&lt;T&gt;</c> for collections, and <c>LocationInfo</c> instead of a
/// <see cref="Location"/> that would pin a syntax tree).
/// </summary>
public class GeneratorIncrementalTests
{
    private const string ValidSource = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed class LootEntry
        {
            [RandomWeight]
            public long Weight { get; init; }
        }
        """;

    private const string DiagnosticSource = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed class LootEntry
        {
            [RandomWeight]
            public decimal Weight { get; init; }
        }
        """;

    private const string ValidSourceWithSharedSourceOverloads = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed class LootEntry
        {
            [RandomWeight(SharedSourceOverloads = true)]
            public long Weight { get; init; }
        }
        """;

    private const string ValidSourceWithEditedBody = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed class LootEntry
        {
            [RandomWeight]
            public long Weight { get; init; }

            public long Doubled()
            {
                // Body added; nothing the model captures changed, so every downstream stage must
                // still be able to reuse its previous output.
                return Weight * 2;
            }
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
        // The "Members" transform necessarily re-runs (the target's syntax tree changed), but the
        // model it produces is identical, so nothing downstream may recompute.
        var (_, second) = GeneratorTest.RunTwice<RandomWeightGenerator>(
            ValidSource, _ => ValidSourceWithEditedBody, GeneratorTestSupport.Options);

        AssertEveryCollectedStageReused(second);
    }

    /// <summary>
    /// The other side of the caching contract: a change the model <em>does</em> capture has to
    /// invalidate it. <c>SharedSourceOverloads</c> only reaches the emitter through
    /// <c>WeightedTypeModel</c>, so if it were left out of the record's members, flipping it would
    /// leave the previous output in place and the new overloads would never appear.
    /// </summary>
    [Fact]
    public void SharedSourceOverloadsFlagFlip_InvalidatesTheEmissionModel()
    {
        var (_, second) = GeneratorTest.RunTwice<RandomWeightGenerator>(
            ValidSource, _ => ValidSourceWithSharedSourceOverloads, GeneratorTestSupport.Options);

        IncrementalAssert.SomeOutputRecomputed(second, RandomWeightGenerator.TrackingNames.Types);
    }

    private static (GeneratorTestResult First, GeneratorTestResult Second) RunWithUnrelatedSyntaxTreeAdded(
        string source) =>
        GeneratorTest.RunTwiceWithCompilationChange<RandomWeightGenerator>(
            source,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))),
            GeneratorTestSupport.Options);

    private static void AssertEveryCollectedStageReused(GeneratorTestResult secondRun) =>
        IncrementalAssert.AllCachedOrUnchanged(
            secondRun,
            RandomWeightGenerator.TrackingNames.CollectedMembers,
            RandomWeightGenerator.TrackingNames.Analysis,
            RandomWeightGenerator.TrackingNames.Types,
            RandomWeightGenerator.TrackingNames.Diagnostics);
}
