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

    private const string PositionalRecordSource = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed record LootEntry(string ItemId, [property: RandomWeight] long Weight);
        """;

    private const string PositionalRecordSourceWithSharedSourceOverloads = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed record LootEntry(string ItemId, [property: RandomWeight(SharedSourceOverloads = true)] long Weight);
        """;

    private const string PositionalRecordSourceWithAddedMember = """
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed record LootEntry(string ItemId, [property: RandomWeight] long Weight)
        {
            public bool IsRare => Weight <= 1;
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

    /// <summary>
    /// The syntax-driven branch has to cache like the attribute-driven one. It cannot lean on
    /// <c>ForAttributeWithMetadataName</c>'s per-tree filtering, so its transform runs wherever a
    /// redirected <c>[RandomWeight]</c> is written, and only the value equality of the model it
    /// produces keeps the stages behind it from recomputing.
    /// </summary>
    [Fact]
    public void PositionalRecordSource_UnrelatedSyntaxTreeAddition_ReusesEveryCollectedStage()
    {
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(PositionalRecordSource);

        AssertEveryCollectedStageReused(second);
    }

    [Fact]
    public void PositionalRecordSource_UnrelatedMemberAddedToTheRecord_ReusesEveryCollectedStage()
    {
        // The redirected transform necessarily re-runs (its syntax tree changed), but the promoted
        // property resolves to the same model, so nothing downstream may recompute.
        var (_, second) = GeneratorTest.RunTwice<RandomWeightGenerator>(
            PositionalRecordSource, _ => PositionalRecordSourceWithAddedMember, GeneratorTestSupport.Options);

        AssertEveryCollectedStageReused(second);
    }

    /// <summary>
    /// The other half of the contract for the new branch: a named argument written on the redirected
    /// attribute has to reach the emission model. It is read from the <c>AttributeData</c> the branch
    /// resolves by matching application syntax, so a branch that found the right symbol but the wrong
    /// application would leave the previous output in place.
    /// </summary>
    [Fact]
    public void PositionalRecordSource_SharedSourceOverloadsFlagFlip_InvalidatesTheEmissionModel()
    {
        var (_, second) = GeneratorTest.RunTwice<RandomWeightGenerator>(
            PositionalRecordSource,
            _ => PositionalRecordSourceWithSharedSourceOverloads,
            GeneratorTestSupport.Options);

        IncrementalAssert.SomeOutputRecomputed(second, RandomWeightGenerator.TrackingNames.Types);
    }

    /// <summary>
    /// Switching the target specifier switches which branch models the member and turns generation
    /// into SSALR007, so both projections off the analysis node have to recompute.
    /// </summary>
    [Fact]
    public void PositionalRecordSource_TargetSpecifierChange_InvalidatesBothProjections()
    {
        var (_, second) = GeneratorTest.RunTwice<RandomWeightGenerator>(
            PositionalRecordSource,
            source => source.Replace("[property:", "[field:", StringComparison.Ordinal),
            GeneratorTestSupport.Options);

        IncrementalAssert.SomeOutputRecomputed(
            second,
            RandomWeightGenerator.TrackingNames.CollectedRedirectedMembers,
            RandomWeightGenerator.TrackingNames.Types,
            RandomWeightGenerator.TrackingNames.Diagnostics);
    }

    private static (GeneratorTestResult First, GeneratorTestResult Second) RunWithUnrelatedSyntaxTreeAdded(
        string source) =>
        GeneratorTest.RunTwiceWithCompilationChange<RandomWeightGenerator>(
            source,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))),
            GeneratorTestSupport.Options);

    /// <summary>
    /// Both branches' batched stages and everything behind them. The per-member transforms are
    /// deliberately left out: they re-run whenever their syntax tree changes, which is normal and not
    /// what these tests are about -- what matters is that the batches they feed compare equal.
    /// </summary>
    private static void AssertEveryCollectedStageReused(GeneratorTestResult secondRun) =>
        IncrementalAssert.AllCachedOrUnchanged(
            secondRun,
            RandomWeightGenerator.TrackingNames.CollectedMembers,
            RandomWeightGenerator.TrackingNames.CollectedRedirectedMembers,
            RandomWeightGenerator.TrackingNames.Analysis,
            RandomWeightGenerator.TrackingNames.Types,
            RandomWeightGenerator.TrackingNames.Diagnostics);
}
