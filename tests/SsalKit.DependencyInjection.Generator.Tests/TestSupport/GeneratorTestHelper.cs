using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SsalKit.DependencyInjection.Generator.Analysis;

namespace SsalKit.DependencyInjection.Generator.Tests.TestSupport;

/// <summary>
/// Builds an in-memory <see cref="CSharpCompilation"/> from source text and drives the real
/// <see cref="ServiceRegistrationGenerator"/>, <see cref="ServiceAttributeAnalyzer"/>,
/// <see cref="ServiceFactoryAnalyzer"/>, and <see cref="RegisterImplementationsOfAnalyzer"/>
/// against it, entirely in-process (no external `dotnet build`/MSBuild involved).
/// </summary>
internal static class GeneratorTestHelper
{
    private static readonly ImmutableArray<MetadataReference> SharedReferences = BuildReferences();

    public static GeneratorRunResult RunGenerator(
        string source,
        string assemblyName = "TestAssembly",
        IEnumerable<MetadataReference>? extraReferences = null,
        bool allowUnsafe = false)
    {
        var compilation = CreateCompilation(source, assemblyName, extraReferences, allowUnsafe);

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

    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        string assemblyName = "TestAssembly",
        IEnumerable<MetadataReference>? extraReferences = null,
        bool allowUnsafe = false)
    {
        var compilation = CreateCompilation(source, assemblyName, extraReferences, allowUnsafe);

        // Every analyzer always runs together, exactly as they do when the package is consumed:
        // whichever attribute a test source uses, the others must stay silent about it.
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
            new ServiceAttributeAnalyzer(), new ServiceFactoryAnalyzer(), new RegisterImplementationsOfAnalyzer()));

        var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();

        // Exclude any incidental compiler diagnostics (e.g. from deliberately-invalid test
        // sources) so tests only assert on SSAL0xx diagnostics unless they opt in.
        return diagnostics.Where(d => d.Id.StartsWith("SSAL", StringComparison.Ordinal)).ToImmutableArray();
    }

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "TestAssembly",
        IEnumerable<MetadataReference>? extraReferences = null,
        bool allowUnsafe = false)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        var references = extraReferences is null
            ? SharedReferences
            : SharedReferences.AddRange(extraReferences);

        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// Compiles <paramref name="source"/> into an in-memory assembly and returns a
    /// <see cref="MetadataReference"/> to it, for tests that need a second, separately-compiled
    /// assembly (e.g. to exercise cross-assembly accessibility rules such as <c>extern alias</c> or
    /// <c>protected internal</c>/<c>[InternalsVisibleTo]</c>).
    /// </summary>
    public static MetadataReference CompileToReference(string source, string assemblyName)
    {
        var compilation = CreateCompilation(source, assemblyName);

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Failed to compile reference assembly '{assemblyName}':{Environment.NewLine}{errors}");
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
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
    /// The assembly-wide registration file (<c>...ServiceCollectionExtensions.g.cs</c>), for runs
    /// that also produce one <c>[ServiceFactory]</c> implementation file per factory interface and
    /// therefore cannot use <see cref="GetSingleSource"/>.
    /// </summary>
    public string GetRegistrationSource() =>
        GeneratedSources.Single(s => s.HintName.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.Ordinal)).Source;

    /// <summary>
    /// The generated source registered under exactly <paramref name="hintName"/>.
    /// </summary>
    public string GetSource(string hintName) =>
        GeneratedSources.Single(s => s.HintName == hintName).Source;

    /// <summary>
    /// Returns the compiler errors (if any) in the compilation *after* the generated source has
    /// been added, proving the emitted code doesn't just look right but actually type-checks
    /// against the real Microsoft.Extensions.DependencyInjection API surface.
    /// </summary>
    public ImmutableArray<Diagnostic> GetOutputCompilationErrors() =>
        OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
}
