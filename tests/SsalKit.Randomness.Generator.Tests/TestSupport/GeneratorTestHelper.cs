using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SsalKit.Randomness.Generator.Tests.TestSupport;

/// <summary>
/// Builds an in-memory <see cref="CSharpCompilation"/> from source text and drives the real
/// <see cref="RandomWeightGenerator"/> against it, entirely in-process (no external
/// `dotnet build`/MSBuild involved).
/// </summary>
internal static class GeneratorTestHelper
{
    private static readonly ImmutableArray<MetadataReference> SharedReferences = BuildReferences();

    public static RandomWeightRunResult RunGenerator(string source, string assemblyName = "TestAssembly")
    {
        var compilation = CreateCompilation(source, assemblyName);

        var generator = new RandomWeightGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = driver.GetRunResult().Results
            .SelectMany(result => result.GeneratedSources)
            .Select(generated => (generated.HintName, Source: generated.SourceText.ToString()))
            .ToImmutableArray();

        return new RandomWeightRunResult(generatedSources, diagnostics, outputCompilation);
    }

    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            SharedReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();

        // Every reference assembly the current (net10.0) test host trusts, which gives a full,
        // correct BCL surface without hand-picking individual assemblies.
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies!.Split(Path.PathSeparator))
            {
                if (File.Exists(path))
                {
                    builder.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        // The real RandomWeightAttribute, IRandomSource, WeightedRandomExtensions, and
        // WeightedSampler<T>, so the generated code is type-checked against the shipping API.
        var randomnessAssembly = typeof(RandomWeightAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(randomnessAssembly))
        {
            builder.Add(MetadataReference.CreateFromFile(randomnessAssembly));
        }

        return builder.ToImmutable();
    }
}

internal sealed record RandomWeightRunResult(
    ImmutableArray<(string HintName, string Source)> GeneratedSources,
    ImmutableArray<Diagnostic> Diagnostics,
    Compilation OutputCompilation)
{
    public string GetSingleSource() => GeneratedSources.Single().Source;

    /// <summary>
    /// The SSALR diagnostics only, so a test can assert on them without filtering out incidental
    /// compiler diagnostics from a deliberately-invalid test source.
    /// </summary>
    public ImmutableArray<Diagnostic> SsalrDiagnostics =>
        Diagnostics.Where(d => d.Id.StartsWith("SSALR", StringComparison.Ordinal)).ToImmutableArray();

    /// <summary>
    /// The compiler errors (if any) in the compilation *after* the generated source was added,
    /// proving the emitted code doesn't just look right but type-checks against the real
    /// SsalKit.Randomness API surface.
    /// </summary>
    public ImmutableArray<Diagnostic> GetOutputCompilationErrors() =>
        OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

    public string AssertCompilesCleanly()
    {
        var errors = GetOutputCompilationErrors();
        Assert.True(
            errors.IsEmpty,
            "Generated code failed to compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

        return GetSingleSource();
    }
}
