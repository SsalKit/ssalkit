using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing.Tests.Harness;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// A generator or analyzer that throws must fail the test, not pass it.
/// </summary>
/// <remarks>
/// Roslyn never lets such an exception escape: it records it on
/// <see cref="GeneratorRunResult.Exception"/> and reports <c>CS8785</c> -- a <em>warning</em> -- for
/// a generator, and <c>AD0001</c> for an analyzer. Neither is an error, so a crashed run leaves a
/// compilation that still compiles cleanly, no generated files, and none of the package's own
/// diagnostics. Every negative assertion a test makes about it therefore passes for the wrong
/// reason, which is the one failure a test harness must not have.
/// </remarks>
public class CrashDetectionTests
{
    private const string AnySource = "public class Widget { }";

    private static readonly GeneratorTestOptions Allowing = new() { AllowGeneratorExceptions = true };

    [Fact]
    public void Run_AGeneratorThatThrows_FailsWithTheExceptionInsteadOfLookingLikeAnEmptyRun()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => GeneratorTest.Run<ThrowingGenerator>(AnySource));

        Assert.Contains(nameof(ThrowingGenerator), exception.Message, StringComparison.Ordinal);
        Assert.Contains(ThrowingGenerator.FailureMessage, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Generator stack trace:", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(GeneratorTestOptions.AllowGeneratorExceptions), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The regression this guards: without the check, all three of these pass over a crashed run.
    /// </summary>
    [Fact]
    public void Run_AGeneratorThatThrows_WouldOtherwisePassEveryNegativeAssertion()
    {
        var result = GeneratorTest.Run<ThrowingGenerator>(AnySource, Allowing);

        result.AssertNoGeneratedSources().AssertCompilesCleanly();
        Assert.Empty(result.GetCompilationErrors());
        Assert.DoesNotContain(
            result.RawResult.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Run_WhenExceptionsAreAllowed_TheRunIsHandedBackWithTheExceptionOnIt()
    {
        var result = GeneratorTest.Run<ThrowingGenerator>(AnySource, Allowing);

        var exception = Assert.Single(result.RawResult.Results).Exception;

        Assert.Equal(ThrowingGenerator.FailureMessage, Assert.IsType<InvalidOperationException>(exception).Message);
    }

    /// <summary>
    /// <c>CS8785</c> is a warning whose id starts with <c>CS</c>, so a package prefix would filter
    /// it out along with the incidental compiler noise the prefix exists to remove -- leaving a
    /// crashed run indistinguishable from a quiet one even for a test that opted into crashes.
    /// </summary>
    [Fact]
    public void Diagnostics_TheGeneratorCrashWarning_SurvivesTheIdPrefixFilter()
    {
        var result = GeneratorTest.Run<ThrowingGenerator>(
            AnySource, Allowing with { DiagnosticIdPrefix = "MINI" });

        Assert.Equal("CS8785", Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void RunTwice_AGeneratorThatThrows_IsCaughtOnTheFirstRunAlready()
    {
        var exception = Assert.Throws<GeneratorAssertionException>(
            () => GeneratorTest.RunTwice<ThrowingGenerator>(AnySource));

        Assert.Contains(ThrowingGenerator.FailureMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunTwiceWithCompilationChange_WhenExceptionsAreAllowed_BothRunsComeBack()
    {
        var (first, second) = GeneratorTest.RunTwiceWithCompilationChange<ThrowingGenerator>(
            AnySource,
            static compilation => compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("// unrelated")),
            Allowing);

        Assert.NotNull(Assert.Single(first.RawResult.Results).Exception);
        Assert.NotNull(Assert.Single(second.RawResult.Results).Exception);
    }

    [Fact]
    public void Run_AWellBehavedGenerator_IsNotAffected() =>
        Assert.Single(GeneratorTest.Run<MiniGenerator>(TestSources.OneMarkedType).GeneratedSources);

    [Fact]
    public async Task RunAnalyzerAsync_AnAnalyzerThatThrows_FailsInsteadOfReportingNothing()
    {
        var exception = await Assert.ThrowsAsync<GeneratorAssertionException>(
            () => GeneratorTest.RunAnalyzerAsync<ThrowingAnalyzer>(AnySource));

        Assert.Contains("analyzer(s) threw", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrowingAnalyzer), exception.Message, StringComparison.Ordinal);
        Assert.Contains(ThrowingAnalyzer.FailureMessage, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A crashed analyzer run alongside working ones is the dangerous shape: the others still report,
    /// so the run looks healthy and only the crashed analyzer's own rule silently never fires.
    /// </summary>
    [Fact]
    public async Task RunAnalyzersAsync_OneAnalyzerOfASetThrowing_StillFails()
    {
        var exception = await Assert.ThrowsAsync<GeneratorAssertionException>(
            () => GeneratorTest.RunAnalyzersAsync(
                "public class BadThing { }",
                [new BadNameAnalyzer(), new ThrowingAnalyzer()],
                new GeneratorTestOptions { DiagnosticIdPrefix = "MINI" }));

        Assert.Contains(ThrowingAnalyzer.FailureMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAnalyzerAsync_WhenExceptionsAreAllowed_TheCrashIsReportedAsAD0001()
    {
        var diagnostics = await GeneratorTest.RunAnalyzerAsync<ThrowingAnalyzer>(AnySource, Allowing);

        DiagnosticAssert.Single(diagnostics, "AD0001");
    }

    /// <summary>
    /// <c>AD0001</c> shares no prefix with any package's own ids, so the filter would drop it too.
    /// </summary>
    [Fact]
    public async Task RunAnalyzerAsync_TheAnalyzerCrashDiagnostic_SurvivesTheIdPrefixFilter()
    {
        var diagnostics = await GeneratorTest.RunAnalyzerAsync<ThrowingAnalyzer>(
            AnySource, Allowing with { DiagnosticIdPrefix = "MINI" });

        Assert.Equal("AD0001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task RunAnalyzerAsync_AWellBehavedAnalyzer_IsNotAffected()
    {
        var diagnostics = await GeneratorTest.RunAnalyzerAsync<BadNameAnalyzer>(
            "public class BadThing { }", new GeneratorTestOptions { DiagnosticIdPrefix = "MINI" });

        DiagnosticAssert.Single(diagnostics, "MINI900");
    }
}
