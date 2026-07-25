using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing.Tests.Harness;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// The entry points: building a compilation, compiling a second assembly to reference, and driving
/// a generator or an analyzer over the result.
/// </summary>
public class GeneratorTestTests
{
    [Fact]
    public void Run_EmitsOneFilePerMarkedType_AndTheOutputCompiles()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Equal("Widget.Mini.g.cs", Assert.Single(result.GeneratedSources).HintName);
        Assert.Contains("WidgetGreeter", result.AssertCompilesCleanlyAndGetSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_SourceWithNothingToGenerateFrom_ProducesNoFiles() =>
        GeneratorTest.Run<MiniGenerator>(TestSources.NoMarkedType).AssertNoGeneratedSources();

    [Fact]
    public void Run_ExposesTheGeneratorsDiagnostics()
    {
        var result = GeneratorTest.Run<MiniGenerator>(TestSources.BadlyNamedType);

        DiagnosticAssert.Single(result.Diagnostics, "MINI001", DiagnosticSeverity.Error);
    }

    [Fact]
    public void CreateCompilation_NullSource_IsARejectedMisuse() =>
        Assert.Throws<ArgumentNullException>(() => GeneratorTest.CreateCompilation(null!));

    [Fact]
    public void CreateCompilation_ReferencesTheTestHostsOwnAssemblies()
    {
        var compilation = GeneratorTest.CreateCompilation("public class C { public System.Uri? Uri; }");

        Assert.Empty(ErrorsOf(compilation));
    }

    [Fact]
    public void CompileToReference_ProducesAnAssemblyTheCompilationUnderTestCanUse()
    {
        var reference = GeneratorTest.CompileToReference(
            "namespace Contracts { public interface IThing { } }", "Contracts");

        var compilation = GeneratorTest.CreateCompilation(
            "public class Thing : Contracts.IThing { }",
            new GeneratorTestOptions { AdditionalReferences = [reference] });

        Assert.Empty(ErrorsOf(compilation));
    }

    [Fact]
    public void CompileToReference_SourceThatDoesNotCompile_NamesTheAssemblyAndTheErrors()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => GeneratorTest.CompileToReference("public class Broken { int X => \"s\"; }", "BrokenLib"));

        Assert.Contains("BrokenLib", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CS0029", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompileToReference_WithoutAnAssemblyName_IsARejectedMisuse() =>
        Assert.Throws<ArgumentException>(() => GeneratorTest.CompileToReference("class C { }", ""));

    [Fact]
    public void RunTwice_NullSource_IsARejectedMisuse() =>
        Assert.Throws<ArgumentNullException>(() => GeneratorTest.RunTwice<MiniGenerator>(null!));

    [Fact]
    public void RunTwice_WithoutAMutation_ReParsesTheIdenticalSourceAndReusesEveryStage()
    {
        var (first, second) = GeneratorTest.RunTwice<MiniGenerator>(TestSources.OneMarkedType);

        Assert.Equal(first.GetSingleSource(), second.GetSingleSource());
        IncrementalAssert.AllCachedOrUnchanged(
            second, MiniGenerator.TrackingNames.Models, MiniGenerator.TrackingNames.Collected);
    }

    [Fact]
    public void RunTwice_WithAnEditTheModelCaptures_FlowsThroughToTheSecondRunsOutput()
    {
        var (first, second) = GeneratorTest.RunTwice<MiniGenerator>(
            TestSources.OneMarkedType, static _ => TestSources.OneMarkedTypeWithOtherGreeting);

        Assert.Contains("hello", first.GetSingleSource(), StringComparison.Ordinal);
        Assert.Contains("goodbye", second.GetSingleSource(), StringComparison.Ordinal);
        IncrementalAssert.SomeOutputRecomputed(second, MiniGenerator.TrackingNames.Models);
    }

    [Fact]
    public void RunTwice_WithANewlyMarkedType_ProducesTheAdditionalFile()
    {
        var (first, second) = GeneratorTest.RunTwice<MiniGenerator>(
            TestSources.OneMarkedType, static _ => TestSources.TwoMarkedTypes);

        Assert.Single(first.GeneratedSources);
        Assert.Equal(2, second.GeneratedSources.Length);
        IncrementalAssert.SomeOutputRecomputed(second, MiniGenerator.TrackingNames.Collected);
    }

    [Fact]
    public void RunTwiceWithCompilationChange_NullChange_IsARejectedMisuse() =>
        Assert.Throws<ArgumentNullException>(
            () => GeneratorTest.RunTwiceWithCompilationChange<MiniGenerator>(TestSources.OneMarkedType, null!));

    [Fact]
    public void RunTwiceWithCompilationChange_AnUnrelatedSyntaxTree_ReusesEveryStage()
    {
        var (_, second) = GeneratorTest.RunTwiceWithCompilationChange<MiniGenerator>(
            TestSources.OneMarkedType,
            static compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// nothing to do with the generator")));

        IncrementalAssert.AllCachedOrUnchanged(
            second, MiniGenerator.TrackingNames.Models, MiniGenerator.TrackingNames.Collected);
    }

    [Fact]
    public async Task RunAnalyzerAsync_ReportsTheAnalyzersOwnDiagnostics()
    {
        var diagnostics = await GeneratorTest.RunAnalyzerAsync<BadNameAnalyzer>(
            "public class BadThing { }", new GeneratorTestOptions { DiagnosticIdPrefix = "MINI" });

        DiagnosticAssert.Single(diagnostics, "MINI900", DiagnosticSeverity.Warning, locatedOnSnippet: "BadThing");
    }

    [Fact]
    public async Task RunAnalyzersAsync_RunsEveryAnalyzerTogether()
    {
        var diagnostics = await GeneratorTest.RunAnalyzersAsync(
            "public class BadThing { } public class OddThing { }",
            [new BadNameAnalyzer(), new OddNameAnalyzer()],
            new GeneratorTestOptions { DiagnosticIdPrefix = "MINI" });

        Assert.Equal(2, diagnostics.Length);
        DiagnosticAssert.Single(diagnostics, "MINI900");
        DiagnosticAssert.Single(diagnostics, "MINI901");
    }

    [Fact]
    public async Task RunAnalyzersAsync_WithoutAPrefix_AlsoReturnsTheCompilersOwnDiagnostics()
    {
        var diagnostics = await GeneratorTest.RunAnalyzersAsync(
            "public class BadThing { private int unused; }", [new BadNameAnalyzer()]);

        DiagnosticAssert.Single(diagnostics, "MINI900");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id.StartsWith("CS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAnalyzersAsync_NullAnalyzers_IsARejectedMisuse() =>
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => GeneratorTest.RunAnalyzersAsync("class C { }", null!));

    [Fact]
    public async Task RunAnalyzersAsync_WithoutAnalyzers_IsARejectedMisuse()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => GeneratorTest.RunAnalyzersAsync("class C { }", []));

        Assert.Equal("analyzers", exception.ParamName);
    }

    private static IEnumerable<Diagnostic> ErrorsOf(Compilation compilation) =>
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
