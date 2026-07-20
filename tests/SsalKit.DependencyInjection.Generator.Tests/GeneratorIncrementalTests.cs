using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies that <see cref="ServiceRegistrationGenerator"/>'s incremental pipeline actually
/// caches: an unrelated compilation change (a new syntax tree with no <c>[Service]</c>
/// attributes) must not force the class-registration stages to recompute, which relies on
/// <c>EquatableArray{T}</c>/record value-equality on the models flowing through the pipeline.
/// </summary>
public class GeneratorIncrementalTests
{
    private const string Source = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNs;

        public interface IFoo { }

        [Service]
        public class Foo : IFoo { }
        """;

    [Fact]
    public void UnrelatedSyntaxTreeAddition_ReusesCollectedClassesAndCombinedSteps()
    {
        var generator = new ServiceRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        var compilation1 = GeneratorTestHelper.CreateCompilation(Source);
        driver = driver.RunGenerators(compilation1);

        // A meaning-nothing change: add a brand new syntax tree with no [Service] attributes.
        // The class-registration pipeline's output should be entirely unaffected.
        var compilation2 = compilation1.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest)));
        driver = driver.RunGenerators(compilation2);

        var runResult = driver.GetRunResult();
        var trackedSteps = runResult.Results.Single().TrackedSteps;

        AssertAllOutputsCachedOrUnchanged(trackedSteps, ServiceRegistrationGenerator.TrackingNames.CollectedClasses);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, ServiceRegistrationGenerator.TrackingNames.Combined);
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
