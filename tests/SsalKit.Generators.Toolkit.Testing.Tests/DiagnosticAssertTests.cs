using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit.Testing.Tests.Harness;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// Diagnostic assertions, including the location check that lets a test name a position by a
/// snippet of the source instead of by a line and column that drifts with every edit above it.
/// </summary>
public class DiagnosticAssertTests
{
    private const string AnalyzerSource = "public class BadThing { }";

    [Fact]
    public void Single_ReturnsTheMatchSoTheMessageCanBeAssertedToo()
    {
        var diagnostics = DiagnosticsOf(TestSources.BadlyNamedType);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "MINI001");

        Assert.Contains("BadWidget", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Single_WhenNothingWasReported_SaysSo()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.Single(DiagnosticsOf(TestSources.OneMarkedType), "MINI001"));

        Assert.Contains("but found 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Reported diagnostics: (none)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_WhenTheSameIdWasReportedTwice_ListsBoth()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.Single(DiagnosticsOf(TestSources.TwoBadlyNamedTypes), "MINI001"));

        Assert.Contains("but found 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("BadWidget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("BadGadget", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_ChecksSeverity_SoAnIdCannotBeSilentlyDowngraded()
    {
        var diagnostics = DiagnosticsOf(TestSources.BadlyNamedType);

        DiagnosticAssert.Single(diagnostics, "MINI001", DiagnosticSeverity.Error);

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.Single(diagnostics, "MINI001", DiagnosticSeverity.Warning));

        Assert.Contains("reported as Warning, but it was reported as Error", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_Exclusive_PassesWhenTheExpectedDiagnosticWasTheOnlyOne()
    {
        var diagnostics = DiagnosticsOf(TestSources.BadlyNamedType);

        var diagnostic = DiagnosticAssert.Single(
            diagnostics, "MINI001", DiagnosticSeverity.Error, exclusive: true);

        Assert.Contains("BadWidget", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Single_Exclusive_RejectsASecondDiagnosticTheNonExclusiveFormWouldLetThrough()
    {
        var diagnostics = DiagnosticsOf(TestSources.BadAndOddlyNamedTypes);

        // Exactly one MINI001 was reported, so the non-exclusive form is satisfied ...
        DiagnosticAssert.Single(diagnostics, "MINI001");

        // ... but MINI002 came along with it.
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.Single(diagnostics, "MINI001", exclusive: true));

        Assert.Contains(
            "to be the only diagnostic reported, but 2 were reported", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MINI002 (Warning)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_WithoutAnId_IsARejectedMisuse() =>
        Assert.Throws<ArgumentException>(() => DiagnosticAssert.Single([], ""));

    [Fact]
    public void Single_ChecksTheLocationWhenASnippetIsGiven()
    {
        var diagnostics = DiagnosticsOf(TestSources.BadlyNamedType);

        DiagnosticAssert.Single(
            diagnostics,
            "MINI001",
            DiagnosticSeverity.Error,
            locatedOnSnippet: """Mini.Marker("hello")""",
            source: TestSources.BadlyNamedType);
    }

    [Fact]
    public void None_PassesWhenThePrefixIsAbsentAndListsTheOffendersWhenItIsNot()
    {
        DiagnosticAssert.None(DiagnosticsOf(TestSources.OneMarkedType), "MINI");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.None(DiagnosticsOf(TestSources.BadAndOddlyNamedTypes), "MINI"));

        Assert.Contains("but found 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MINI001 (Error)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MINI002 (Warning)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void None_WithoutAPrefix_IsARejectedMisuse() =>
        Assert.Throws<ArgumentException>(() => DiagnosticAssert.None([], ""));

    [Fact]
    public async Task LocatedOn_FindsTheSourceItselfWhenTheLocationCarriesItsSyntaxTree()
    {
        var diagnostic = DiagnosticAssert.Single(await AnalyzerDiagnosticsAsync(AnalyzerSource), "MINI900");

        DiagnosticAssert.LocatedOn(diagnostic, "BadThing");
    }

    [Fact]
    public void LocatedOn_ADiagnosticWithNoLocationAtAll_SaysSoRatherThanPassingVacuously()
    {
        var diagnostic = DiagnosticAssert.Single(DiagnosticsOf(TestSources.OddlyNamedType), "MINI002");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.LocatedOn(diagnostic, "OddWidget", TestSources.OddlyNamedType));

        Assert.Contains("without a source location at all", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocatedOn_AGeneratorDiagnosticWithoutItsTree_AsksForTheSource()
    {
        var diagnostic = DiagnosticAssert.Single(DiagnosticsOf(TestSources.BadlyNamedType), "MINI001");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.LocatedOn(diagnostic, "BadWidget"));

        Assert.Contains("does not carry a syntax tree", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'source' parameter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocatedOn_ASnippetThatIsNotInTheSource_SaysWhereTheDiagnosticActuallyIs()
    {
        var diagnostic = DiagnosticAssert.Single(await AnalyzerDiagnosticsAsync(AnalyzerSource), "MINI900");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.LocatedOn(diagnostic, "NotInTheSource"));

        Assert.Contains("does not occur in the source", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocatedOn_ASnippetThatOccursTwice_AsksForAUniqueOne()
    {
        var diagnostic = DiagnosticAssert.Single(
            await AnalyzerDiagnosticsAsync("public class BadThing { } public class Other { }"), "MINI900");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.LocatedOn(diagnostic, "public class"));

        Assert.Contains("occurs more than once", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Extend it until it is unique", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocatedOn_ADiagnosticSomewhereElse_QuotesWhatItActuallyCovers()
    {
        const string source = "public class BadThing { public int Elsewhere; }";
        var diagnostic = DiagnosticAssert.Single(await AnalyzerDiagnosticsAsync(source), "MINI900");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.LocatedOn(diagnostic, "Elsewhere"));

        Assert.Contains("which covers 'BadThing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocatedOn_ASourceThatDoesNotEvenSpanTheDiagnostic_SaysSoInsteadOfCrashing()
    {
        var diagnostic = DiagnosticAssert.Single(await AnalyzerDiagnosticsAsync(AnalyzerSource), "MINI900");

        var exception = Assert.Throws<GeneratorAssertionException>(
            () => DiagnosticAssert.LocatedOn(diagnostic, "BadThing", "xyzBadThing"));

        Assert.Contains("a span outside this source text", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocatedOn_NullArguments_AreRejectedMisuse()
    {
        var diagnostic = DiagnosticAssert.Single(await AnalyzerDiagnosticsAsync(AnalyzerSource), "MINI900");

        Assert.Throws<ArgumentNullException>(() => DiagnosticAssert.LocatedOn(null!, "BadThing"));
        Assert.Throws<ArgumentException>(() => DiagnosticAssert.LocatedOn(diagnostic, ""));
    }

    private static ImmutableArray<Diagnostic> DiagnosticsOf(string source) =>
        GeneratorTest.Run<MiniGenerator>(source).Diagnostics;

    private static Task<ImmutableArray<Diagnostic>> AnalyzerDiagnosticsAsync(string source) =>
        GeneratorTest.RunAnalyzerAsync<BadNameAnalyzer>(
            source, new GeneratorTestOptions { DiagnosticIdPrefix = "MINI" });
}
