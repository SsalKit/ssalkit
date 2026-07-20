using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SsalKit.DependencyInjection.Generator.Analysis;

namespace SsalKit.DependencyInjection.Generator.Tests.TestSupport;

/// <summary>
/// Builds an in-memory <see cref="CSharpCompilation"/> from source text and drives the real
/// <see cref="ServiceRegistrationGenerator"/> and <see cref="ServiceAttributeAnalyzer"/> against
/// it, entirely in-process (no external `dotnet build`/MSBuild involved).
/// </summary>
internal static class GeneratorTestHelper
{
    private static readonly ImmutableArray<MetadataReference> SharedReferences = BuildReferences();

    public static GeneratorRunResult RunGenerator(string source, string assemblyName = "TestAssembly")
    {
        var compilation = CreateCompilation(source, assemblyName);

        var generator = new ServiceRegistrationGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();

        var generatedTrees = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => (s.HintName, Source: s.SourceText.ToString()))
            .ToImmutableArray();

        return new GeneratorRunResult(generatedTrees, diagnostics, outputCompilation);
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source, string assemblyName = "TestAssembly")
    {
        var compilation = CreateCompilation(source, assemblyName);
        var analyzer = new ServiceAttributeAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();

        // Exclude any incidental compiler diagnostics (e.g. from deliberately-invalid test
        // sources) so tests only assert on SSAL0xx diagnostics unless they opt in.
        return diagnostics.Where(d => d.Id.StartsWith("SSAL", StringComparison.Ordinal)).ToImmutableArray();
    }

    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            SharedReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();

        // Pull in every reference assembly the current (net10.0) test host trusts, which gives us
        // a full, correct BCL surface (System.Private.CoreLib, System.Runtime, System.Collections,
        // etc.) without needing to hand-pick individual assemblies or take a dependency on a
        // reference-assembly package.
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

        // The real SsalKit.DependencyInjection.ServiceAttribute/RegistrationMode types, and
        // transitively Microsoft.Extensions.DependencyInjection.Abstractions
        // (IServiceCollection, ServiceLifetime, ServiceDescriptor, keyed-service extensions, ...).
        AddAssemblyOf<SsalKit.DependencyInjection.ServiceAttribute>(builder);
        AddAssemblyOf<Microsoft.Extensions.DependencyInjection.ServiceLifetime>(builder);
        AddAssemblyLocation(builder, typeof(Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions).Assembly.Location);

        return builder.ToImmutable();
    }

    private static void AddAssemblyOf<T>(ImmutableArray<MetadataReference>.Builder builder) =>
        AddAssemblyLocation(builder, typeof(T).Assembly.Location);

    private static void AddAssemblyLocation(ImmutableArray<MetadataReference>.Builder builder, string location)
    {
        if (!string.IsNullOrEmpty(location))
        {
            builder.Add(MetadataReference.CreateFromFile(location));
        }
    }
}

internal sealed record GeneratorRunResult(
    ImmutableArray<(string HintName, string Source)> GeneratedSources,
    ImmutableArray<Diagnostic> Diagnostics,
    Compilation OutputCompilation)
{
    public string GetSingleSource() => GeneratedSources.Single().Source;

    /// <summary>
    /// Returns the compiler errors (if any) in the compilation *after* the generated source has
    /// been added, proving the emitted code doesn't just look right but actually type-checks
    /// against the real Microsoft.Extensions.DependencyInjection API surface.
    /// </summary>
    public ImmutableArray<Diagnostic> GetOutputCompilationErrors() =>
        OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
}
