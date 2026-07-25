using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing.Tests.Harness;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// The differentiating assertion, checked in both directions: a generator that caches must pass
/// <see cref="IncrementalAssert.AllCachedOrUnchanged"/> and fail
/// <see cref="IncrementalAssert.SomeOutputRecomputed"/>, and one that does not must do the
/// opposite. An assertion that only ever passes would prove nothing.
/// </summary>
public class IncrementalAssertTests
{
    [Fact]
    public void AllCachedOrUnchanged_AGeneratorThatCaches_Passes()
    {
        IncrementalAssert.AllCachedOrUnchanged(
            SecondRunOfCachingGenerator(), MiniGenerator.TrackingNames.Models, MiniGenerator.TrackingNames.Collected);
    }

    [Fact]
    public void AllCachedOrUnchanged_AGeneratorWhoseModelWrapsTheCompilation_FailsWithTheCacheState()
    {
        var (_, second) = GeneratorTest.RunTwiceWithCompilationChange<LeakyGenerator>(
            "public class C { }",
            static compilation => compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("// unrelated")));

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => IncrementalAssert.AllCachedOrUnchanged(second, LeakyGenerator.TrackingNames.Models));

        Assert.Contains("'LeakyModels'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Cache state of the requested steps:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("LeakyModels[0] -> Modified", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Tracking names recorded by this run:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllCachedOrUnchanged_ATrackingNameTheGeneratorNeverUses_SaysWhichNamesItDoesUse()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => IncrementalAssert.AllCachedOrUnchanged(SecondRunOfCachingGenerator(), "NoSuchStage"));

        Assert.Contains("No tracked steps were recorded for 'NoSuchStage'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("NoSuchStage -> (never tracked)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("- " + MiniGenerator.TrackingNames.Models, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllCachedOrUnchanged_WithoutTrackingNames_IsARejectedMisuse()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => IncrementalAssert.AllCachedOrUnchanged(SecondRunOfCachingGenerator()));

        Assert.Equal("trackingNames", exception.ParamName);
    }

    [Fact]
    public void AllCachedOrUnchanged_NullArguments_AreRejectedMisuse()
    {
        Assert.Throws<ArgumentNullException>(() => IncrementalAssert.AllCachedOrUnchanged(null!, "Models"));
        Assert.Throws<ArgumentNullException>(
            () => IncrementalAssert.AllCachedOrUnchanged(SecondRunOfCachingGenerator(), null!));
    }

    [Fact]
    public void SomeOutputRecomputed_AnEditTheModelCaptures_Passes()
    {
        var (_, second) = GeneratorTest.RunTwice<MiniGenerator>(
            TestSources.OneMarkedType, static _ => TestSources.OneMarkedTypeWithOtherGreeting);

        IncrementalAssert.SomeOutputRecomputed(
            second, MiniGenerator.TrackingNames.Models, MiniGenerator.TrackingNames.Collected);
    }

    [Fact]
    public void SomeOutputRecomputed_WhenNothingActuallyChanged_FailsWithTheCacheState()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => IncrementalAssert.SomeOutputRecomputed(
                SecondRunOfCachingGenerator(), MiniGenerator.TrackingNames.Models));

        Assert.Contains("every output was reused", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MiniModels[0] -> ", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MiniModels[0] -> Modified", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SomeOutputRecomputed_ATrackingNameTheGeneratorNeverUses_SaysWhichNamesItDoesUse()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => IncrementalAssert.SomeOutputRecomputed(SecondRunOfCachingGenerator(), "NoSuchStage"));

        Assert.Contains("No tracked steps were recorded for 'NoSuchStage'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SomeOutputRecomputed_WithoutTrackingNames_IsARejectedMisuse()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => IncrementalAssert.SomeOutputRecomputed(SecondRunOfCachingGenerator()));

        Assert.Equal("trackingNames", exception.ParamName);
    }

    [Fact]
    public void SomeOutputRecomputed_NullArguments_AreRejectedMisuse()
    {
        Assert.Throws<ArgumentNullException>(() => IncrementalAssert.SomeOutputRecomputed(null!, "Models"));
        Assert.Throws<ArgumentNullException>(
            () => IncrementalAssert.SomeOutputRecomputed(SecondRunOfCachingGenerator(), null!));
    }

    /// <summary>
    /// A second run whose only change is an unrelated syntax tree, so the marked type's own stages
    /// are skipped outright (<c>Cached</c>) and the collected stage is re-run to an equal value
    /// (<c>Unchanged</c>).
    /// </summary>
    private static GeneratorTestResult SecondRunOfCachingGenerator() =>
        GeneratorTest.RunTwiceWithCompilationChange<MiniGenerator>(
            TestSources.OneMarkedType,
            static compilation => compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("// unrelated"))).Second;
}
