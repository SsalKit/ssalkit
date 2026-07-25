using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing.Tests.Harness;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// Each option has to actually reach the compilation, so every one is asserted through a source
/// that only compiles (or only stays quiet) when the option took effect.
/// </summary>
public class GeneratorTestOptionsTests
{
    [Fact]
    public void Default_IsAClassLibraryCalledTestAssemblyWithNullableEnabled()
    {
        var options = GeneratorTestOptions.Default;

        Assert.Equal("TestAssembly", options.AssemblyName);
        Assert.Equal(LanguageVersion.Latest, options.LanguageVersion);
        Assert.Equal(NullableContextOptions.Enable, options.NullableContextOptions);
        Assert.Equal(OutputKind.DynamicallyLinkedLibrary, options.OutputKind);
        Assert.False(options.AllowUnsafe);
        Assert.Null(options.DiagnosticIdPrefix);
        Assert.False(options.SortGeneratedSourcesByHintName);
    }

    [Fact]
    public void AssemblyName_NamesTheCompilationUnderTest()
    {
        var compilation = GeneratorTest.CreateCompilation(
            "class C { }", new GeneratorTestOptions { AssemblyName = "Renamed" });

        Assert.Equal("Renamed", compilation.AssemblyName);
    }

    [Fact]
    public void LanguageVersion_ConstrainsWhatTheTestSourceMayUse()
    {
        const string fileScopedNamespace = "namespace Demo; public class C { }";

        Assert.Empty(ErrorsOf(GeneratorTest.CreateCompilation(fileScopedNamespace)));
        Assert.NotEmpty(ErrorsOf(GeneratorTest.CreateCompilation(
            fileScopedNamespace, new GeneratorTestOptions { LanguageVersion = LanguageVersion.CSharp9 })));
    }

    [Fact]
    public void NullableContextOptions_DecidesWhetherNullabilityIsCheckedAtAll()
    {
        const string source = "public class C { public string Name = null; }";

        Assert.NotEmpty(WarningsOf(GeneratorTest.CreateCompilation(source)));
        Assert.Empty(WarningsOf(GeneratorTest.CreateCompilation(
            source, new GeneratorTestOptions { NullableContextOptions = NullableContextOptions.Disable })));
    }

    [Fact]
    public void OutputKind_DecidesWhetherAnEntryPointIsRequired()
    {
        const string source = "public class C { }";

        Assert.Empty(ErrorsOf(GeneratorTest.CreateCompilation(source)));
        Assert.Contains(
            ErrorsOf(GeneratorTest.CreateCompilation(
                source, new GeneratorTestOptions { OutputKind = OutputKind.ConsoleApplication })),
            diagnostic => diagnostic.Id == "CS5001");
    }

    [Fact]
    public void AllowUnsafe_DecidesWhetherUnsafeCodeCompiles()
    {
        const string source = "public unsafe class C { public int* Pointer; }";

        Assert.NotEmpty(ErrorsOf(GeneratorTest.CreateCompilation(source)));
        Assert.Empty(ErrorsOf(GeneratorTest.CreateCompilation(
            source, new GeneratorTestOptions { AllowUnsafe = true })));
    }

    [Fact]
    public void AdditionalAssemblies_AddOneReferenceEach()
    {
        var baseline = GeneratorTest.CreateCompilation("class C { }").References.Count();

        var withExtra = GeneratorTest.CreateCompilation(
                "class C { }",
                new GeneratorTestOptions { AdditionalAssemblies = [typeof(MiniGenerator).Assembly] })
            .References.Count();

        Assert.Equal(baseline + 1, withExtra);
    }

    [Fact]
    public void AdditionalReferencesAndAssemblies_DefaultToEmptyEvenWhenSetToAnUninitializedArray()
    {
        var options = new GeneratorTestOptions { AdditionalReferences = default, AdditionalAssemblies = default };

        Assert.True(options.AdditionalReferences.IsEmpty);
        Assert.True(options.AdditionalAssemblies.IsEmpty);
        Assert.Empty(ErrorsOf(GeneratorTest.CreateCompilation("class C { }", options)));
    }

    [Fact]
    public void DiagnosticIdPrefix_NarrowsTheReportedDiagnostics()
    {
        var kept = GeneratorTest.Run<MiniGenerator>(
            TestSources.BadlyNamedType, new GeneratorTestOptions { DiagnosticIdPrefix = "MINI" });
        var dropped = GeneratorTest.Run<MiniGenerator>(
            TestSources.BadlyNamedType, new GeneratorTestOptions { DiagnosticIdPrefix = "SSAL" });

        Assert.Equal("MINI001", Assert.Single(kept.Diagnostics).Id);
        Assert.Empty(dropped.Diagnostics);
    }

    [Fact]
    public void SortGeneratedSourcesByHintName_ReordersWhatTheGeneratorProduced()
    {
        var unsorted = GeneratorTest.Run<MiniGenerator>(TestSources.TwoMarkedTypes);
        var sorted = GeneratorTest.Run<MiniGenerator>(
            TestSources.TwoMarkedTypes, new GeneratorTestOptions { SortGeneratedSourcesByHintName = true });

        Assert.Equal(
            ["Zeta.Mini.g.cs", "Alpha.Mini.g.cs"],
            unsorted.GeneratedSources.Select(static generated => generated.HintName));
        Assert.Equal(
            ["Alpha.Mini.g.cs", "Zeta.Mini.g.cs"],
            sorted.GeneratedSources.Select(static generated => generated.HintName));
    }

    [Fact]
    public void With_ProducesAVariantWithoutRepeatingEveryOtherOption()
    {
        var options = GeneratorTestOptions.Default with { AllowUnsafe = true };

        Assert.True(options.AllowUnsafe);
        Assert.Equal("TestAssembly", options.AssemblyName);
        Assert.False(GeneratorTestOptions.Default.AllowUnsafe);
    }

    private static IEnumerable<Diagnostic> ErrorsOf(Compilation compilation) =>
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    private static IEnumerable<Diagnostic> WarningsOf(Compilation compilation) =>
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
}
