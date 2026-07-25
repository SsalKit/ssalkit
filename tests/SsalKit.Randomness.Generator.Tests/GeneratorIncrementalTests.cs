using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        var trackedSteps = RunTwice(
            ValidSource,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))));

        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.CollectedMembers);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Analysis);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Types);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Diagnostics);
    }

    [Fact]
    public void DiagnosticProducingSource_UnrelatedChange_ReusesEveryCollectedStage()
    {
        // A DiagnosticInfo carries a LocationInfo, not a Location: if it held the real thing, the
        // pipeline would compare two runs' diagnostics by reference and never cache.
        var trackedSteps = RunTwice(
            DiagnosticSource,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))));

        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.CollectedMembers);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Analysis);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Types);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Diagnostics);
    }

    [Fact]
    public void UnrelatedMethodAddedToTheDecoratedType_ReusesTheCollectedAndAnalysedStages()
    {
        // The "Members" transform necessarily re-runs (the target's syntax tree changed), but the
        // model it produces is identical, so nothing downstream may recompute.
        var trackedSteps = RunTwice(
            ValidSource,
            compilation => compilation.ReplaceSyntaxTree(
                compilation.SyntaxTrees.Single(),
                CSharpSyntaxTree.ParseText(ValidSourceWithEditedBody, new CSharpParseOptions(LanguageVersion.Latest))));

        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.CollectedMembers);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Analysis);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Types);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, RandomWeightGenerator.TrackingNames.Diagnostics);
    }

    private static ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> RunTwice(
        string source, Func<Compilation, Compilation> change)
    {
        var generator = new RandomWeightGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(change(compilation));

        return driver.GetRunResult().Results.Single().TrackedSteps;
    }

    private static void AssertAllOutputsCachedOrUnchanged(
        ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> trackedSteps,
        string stepName)
    {
        Assert.True(trackedSteps.TryGetValue(stepName, out var steps), $"No tracked steps found for '{stepName}'.");
        Assert.NotEmpty(steps);

        foreach (var step in steps)
        {
            Assert.NotEmpty(step.Outputs);

            foreach (var (_, reason) in step.Outputs)
            {
                Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"Expected step '{stepName}' output reason to be Cached or Unchanged after an unrelated " +
                    $"compilation change, but was '{reason}'.");
            }
        }
    }
}
