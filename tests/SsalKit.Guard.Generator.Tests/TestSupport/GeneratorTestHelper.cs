using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SsalKit.Guard.Generator.Tests.TestSupport;

/// <summary>
/// Builds an in-memory <see cref="CSharpCompilation"/> from source text and drives the real
/// <see cref="ErrorCodesGenerator"/> against it, entirely in-process (no external
/// `dotnet build`/MSBuild involved).
/// </summary>
internal static class GeneratorTestHelper
{
    private static readonly ImmutableArray<MetadataReference> SharedReferences = BuildReferences();

    public static ErrorCodesRunResult RunGenerator(string source, string assemblyName = "TestAssembly")
    {
        var compilation = CreateCompilation(source, assemblyName);

        var generator = new ErrorCodesGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = driver.GetRunResult().Results
            .SelectMany(result => result.GeneratedSources)
            .Select(generated => (generated.HintName, Source: generated.SourceText.ToString()))
            .OrderBy(generated => generated.HintName, StringComparer.Ordinal)
            .ToImmutableArray();

        return new ErrorCodesRunResult(generatedSources, diagnostics, outputCompilation);
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

        // The real ErrorCodedException, [ErrorCode<TCode>], [ErrorCodes<TCode>] and
        // [ExternalErrorCode<TCode>], so the generated code is type-checked against the shipping API.
        var guardAssembly = typeof(SsalKit.Guard.ErrorCodedException).Assembly.Location;
        if (!string.IsNullOrEmpty(guardAssembly))
        {
            builder.Add(MetadataReference.CreateFromFile(guardAssembly));
        }

        return builder.ToImmutable();
    }
}

internal sealed record ErrorCodesRunResult(
    ImmutableArray<(string HintName, string Source)> GeneratedSources,
    ImmutableArray<Diagnostic> Diagnostics,
    Compilation OutputCompilation)
{
    public string GetSingleSource() => GeneratedSources.Single().Source;

    /// <summary>
    /// The SSALG diagnostics only, so a test can assert on them without filtering out incidental
    /// compiler diagnostics from a deliberately-invalid test source.
    /// </summary>
    public ImmutableArray<Diagnostic> SsalgDiagnostics =>
        Diagnostics.Where(d => d.Id.StartsWith("SSALG", StringComparison.Ordinal)).ToImmutableArray();

    /// <summary>
    /// The compiler errors (if any) in the compilation *after* the generated source was added,
    /// proving the emitted code doesn't just look right but type-checks against the real
    /// SsalKit.Guard API surface.
    /// </summary>
    public ImmutableArray<Diagnostic> GetOutputCompilationErrors() =>
        OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

    /// <summary>
    /// Asserts the generated code compiles and returns the single generated file's text, which is
    /// what the snapshot tests snapshot -- so a snapshot can never be updated to something that
    /// merely looks plausible.
    /// </summary>
    public string AssertCompilesCleanly()
    {
        AssertNoCompilationErrors();

        return GetSingleSource();
    }

    /// <summary>
    /// The same check for the multi-container cases, returning every generated file with its hint
    /// name so the snapshot covers both the file names and their contents.
    /// </summary>
    public string AssertCompilesCleanlyWithAllSources()
    {
        AssertNoCompilationErrors();

        return string.Join(
            Environment.NewLine,
            GeneratedSources.Select(generated =>
                "// ==== " + generated.HintName + Environment.NewLine + generated.Source));
    }

    private void AssertNoCompilationErrors()
    {
        var errors = GetOutputCompilationErrors();
        Assert.True(
            errors.IsEmpty,
            "Generated code failed to compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }
}
