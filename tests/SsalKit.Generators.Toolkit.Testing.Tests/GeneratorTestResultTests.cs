using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit.Testing.Tests.Harness;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// What a run hands back, and -- just as importantly -- what its failure messages say when the run
/// did not produce what the test expected.
/// </summary>
public class GeneratorTestResultTests
{
    [Fact]
    public void GetSingleSource_WhenNothingWasGenerated_SaysSoInsteadOfListingNothing()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.NoMarkedType);

        var exception = Assert.Throws<GeneratorAssertionException>(result.GetSingleSource);

        Assert.Contains("produced 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Generated sources: (none)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSingleSource_WhenSeveralFilesWereGenerated_ListsTheirHintNames()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.TwoMarkedTypes);

        var exception = Assert.Throws<GeneratorAssertionException>(result.GetSingleSource);

        Assert.Contains("produced 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("- Zeta.Mini.g.cs", exception.Message, StringComparison.Ordinal);
        Assert.Contains("- Alpha.Mini.g.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSource_MatchesOnAHintNameSuffix()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.TwoMarkedTypes);

        Assert.Contains("AlphaGreeter", result.GetSource("Alpha.Mini.g.cs"), StringComparison.Ordinal);
        Assert.Contains("ZetaGreeter", result.GetSource("Zeta.Mini.g.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void GetSource_ASuffixSeveralFilesShare_IsAmbiguousAndRejected()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.TwoMarkedTypes);

        var exception = Assert.Throws<GeneratorAssertionException>(() => result.GetSource(".Mini.g.cs"));

        Assert.Contains("but found 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSource_ASuffixNoFileHas_NamesWhatWasGenerated()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        var exception = Assert.Throws<GeneratorAssertionException>(() => result.GetSource("Missing.g.cs"));

        Assert.Contains("but found 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("- Widget.Mini.g.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSource_WithoutASuffix_IsARejectedMisuse()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Throws<ArgumentException>(() => result.GetSource(""));
    }

    [Fact]
    public void ToSnapshotText_PutsEachHintNameAboveTheFileItNames()
    {
        var result = GeneratorTest.Run<MiniGenerator>(
            TestSources.TwoMarkedTypes, new GeneratorTestOptions { SortGeneratedSourcesByHintName = true });

        var snapshot = result.ToSnapshotText();

        // The header is a line of its own, so the file it names starts on the next line.
        Assert.StartsWith("// ==== Alpha.Mini.g.cs" + Environment.NewLine, snapshot, StringComparison.Ordinal);
        Assert.Contains("// ==== Zeta.Mini.g.cs" + Environment.NewLine, snapshot, StringComparison.Ordinal);

        // Both files are there, each body after its own header, in GeneratedSources order.
        var alphaHeader = snapshot.IndexOf("// ==== Alpha", StringComparison.Ordinal);
        var alphaBody = snapshot.IndexOf("AlphaGreeter", StringComparison.Ordinal);
        var zetaHeader = snapshot.IndexOf("// ==== Zeta", StringComparison.Ordinal);
        var zetaBody = snapshot.IndexOf("ZetaGreeter", StringComparison.Ordinal);

        Assert.True(alphaHeader < alphaBody && alphaBody < zetaHeader && zetaHeader < zetaBody, snapshot);
    }

    [Fact]
    public void ToSnapshotText_WhenNothingWasGenerated_IsEmptyRatherThanAStrayHeader()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.NoMarkedType);

        Assert.Equal(string.Empty, result.ToSnapshotText());
    }

    [Fact]
    public void AssertCompilesCleanly_ReturnsTheResultSoItCanBeChained()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Same(result, result.AssertCompilesCleanly());
    }

    [Fact]
    public void AssertCompilesCleanly_OutputThatParsesButDoesNotTypeCheck_ReportsTheCompilerErrors()
    {
        var result = GeneratorTest.Run<BrokenOutputGenerator>("public class C { }");

        var exception = Assert.Throws<GeneratorAssertionException>(() => result.AssertCompilesCleanly());

        Assert.Contains("Generated code failed to compile", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CS0029", exception.Message, StringComparison.Ordinal);
        Assert.Contains("- Broken.g.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertCompilesCleanlyAndGetSource_IsTheTwoCallsInOne()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Equal(result.AssertCompilesCleanly().GetSingleSource(), result.AssertCompilesCleanlyAndGetSource());
    }

    [Fact]
    public void AssertCompilesCleanlyAndGetSource_StillReportsAnOutputThatDoesNotCompile()
    {
        var result = GeneratorTest.Run<BrokenOutputGenerator>("public class C { }");

        var exception = Assert.Throws<GeneratorAssertionException>(result.AssertCompilesCleanlyAndGetSource);

        Assert.Contains("Generated code failed to compile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertCompilesCleanlyAndGetSource_WhenSeveralFilesWereGenerated_StillRefusesToPickOne()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.TwoMarkedTypes);

        var exception = Assert.Throws<GeneratorAssertionException>(result.AssertCompilesCleanlyAndGetSource);

        Assert.Contains("produced 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCompilationErrors_IsTheUnwrappedFormOfTheSameCheck()
    {
        var result = GeneratorTest.Run<BrokenOutputGenerator>("public class C { }");

        Assert.Equal("CS0029", Assert.Single(result.GetCompilationErrors()).Id);
    }

    [Fact]
    public void AssertNoGeneratedSources_ReturnsTheResultSoItCanBeChained()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.NoMarkedType);

        Assert.Same(result, result.AssertNoGeneratedSources());
    }

    [Fact]
    public void AssertNoGeneratedSources_WhenSomethingWasGenerated_NamesIt()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        var exception = Assert.Throws<GeneratorAssertionException>(() => result.AssertNoGeneratedSources());

        Assert.Contains("produced 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("- Widget.Mini.g.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputCompilation_ContainsTheGeneratedTree()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Contains(
            result.OutputCompilation.SyntaxTrees,
            tree => tree.FilePath.EndsWith("Widget.Mini.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void RawResult_IsTheEscapeHatchForTheUnfilteredDiagnostics()
    {
        var result = GeneratorTest.Run<MiniGenerator>(
            TestSources.BadlyNamedType, new GeneratorTestOptions { DiagnosticIdPrefix = "NOTHING" });

        Assert.Empty(result.Diagnostics);
        Assert.Equal("MINI001", Assert.Single(result.RawResult.Diagnostics).Id);
    }

    [Fact]
    public void TrackedSteps_IsPopulatedWithoutTheCallerHavingToOptIn()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Contains(MiniGenerator.TrackingNames.Models, result.TrackedSteps.Keys);
        Assert.Contains(MiniGenerator.TrackingNames.Collected, result.TrackedSteps.Keys);
    }

    [Fact]
    public void GeneratedSource_CarriesBothTheHintNameAndTheText()
    {
        var generated = Assert.Single(GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType).GeneratedSources);

        Assert.Equal("Widget.Mini.g.cs", generated.HintName);
        Assert.StartsWith("// <auto-generated/>", generated.Text, StringComparison.Ordinal);
    }
}
