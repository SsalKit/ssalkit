using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// The knobs every <see cref="GeneratorTest"/> entry point shares: how the test compilation is
/// built, what it references, and how the run's output is presented.
/// </summary>
/// <remarks>
/// Every property has a default that suits a generator test, so <c>null</c> options (or
/// <see cref="Default"/>) is the normal case. Instances are immutable records; build a variant with
/// an object initializer or a <c>with</c> expression, and keep one <c>static readonly</c> instance
/// per test project so a single place decides which assemblies the generated code is type-checked
/// against.
/// </remarks>
public sealed record GeneratorTestOptions
{
    private readonly ImmutableArray<MetadataReference> _additionalReferences = [];
    private readonly ImmutableArray<Assembly> _additionalAssemblies = [];

    /// <summary>
    /// The options used whenever a caller passes <c>null</c>: a <c>TestAssembly</c> class library,
    /// the latest language version, nullable reference types enabled, unsafe code disabled, and the
    /// test host's own reference assemblies as the only references.
    /// </summary>
    public static GeneratorTestOptions Default { get; } = new();

    /// <summary>
    /// The name given to the compilation under test. Generators that key on the assembly name (for
    /// example to name the file or extension class they emit) need this set deliberately.
    /// </summary>
    public string AssemblyName { get; init; } = "TestAssembly";

    /// <summary>
    /// The language version the test source -- and any source the generator adds -- is parsed with.
    /// </summary>
    public LanguageVersion LanguageVersion { get; init; } = LanguageVersion.Latest;

    /// <summary>
    /// Whether the compilation under test has nullable reference types enabled. Defaults to
    /// <see cref="Microsoft.CodeAnalysis.NullableContextOptions.Enable"/>, which is what makes
    /// <c>AssertCompilesCleanly</c> meaningful for generated code that annotates its API.
    /// </summary>
    public NullableContextOptions NullableContextOptions { get; init; } = NullableContextOptions.Enable;

    /// <summary>
    /// The kind of assembly the compilation under test produces. Defaults to
    /// <see cref="Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary"/>; a test whose source
    /// declares an entry point needs
    /// <see cref="Microsoft.CodeAnalysis.OutputKind.ConsoleApplication"/>.
    /// </summary>
    public OutputKind OutputKind { get; init; } = OutputKind.DynamicallyLinkedLibrary;

    /// <summary>
    /// Whether the compilation under test allows <c>unsafe</c> code.
    /// </summary>
    public bool AllowUnsafe { get; init; }

    /// <summary>
    /// Extra metadata references added on top of the test host's own reference assemblies -- most
    /// often the result of <see cref="GeneratorTest.CompileToReference"/>, for tests that need a
    /// second, separately compiled assembly (cross-assembly accessibility, <c>extern alias</c>,
    /// <c>[InternalsVisibleTo]</c>).
    /// </summary>
    /// <remarks>An uninitialized (<c>default</c>) array is normalized to an empty one.</remarks>
    public ImmutableArray<MetadataReference> AdditionalReferences
    {
        get => _additionalReferences;
        init => _additionalReferences = value.IsDefault ? [] : value;
    }

    /// <summary>
    /// Extra references named by already-loaded assemblies, so a test project can write
    /// <c>[typeof(MyAttribute).Assembly]</c> instead of resolving a file path. This is how the
    /// generated code gets type-checked against the shipping runtime package rather than against a
    /// stub declared in the test source.
    /// </summary>
    /// <remarks>An uninitialized (<c>default</c>) array is normalized to an empty one.</remarks>
    public ImmutableArray<Assembly> AdditionalAssemblies
    {
        get => _additionalAssemblies;
        init => _additionalAssemblies = value.IsDefault ? [] : value;
    }

    /// <summary>
    /// When set, <see cref="GeneratorTestResult.Diagnostics"/> and the analyzer entry points return
    /// only diagnostics whose id starts with this prefix.
    /// </summary>
    /// <remarks>
    /// This exists so a test can assert on the generator's own diagnostics without first filtering
    /// out incidental compiler diagnostics coming from a deliberately invalid test source. The
    /// unfiltered generator diagnostics remain available through
    /// <see cref="GeneratorTestResult.RawResult"/>.
    /// </remarks>
    public string? DiagnosticIdPrefix { get; init; }

    /// <summary>
    /// Whether <see cref="GeneratorTestResult.GeneratedSources"/> is ordered by hint name instead of
    /// by the order the generator produced the files in. Useful when a snapshot covers every
    /// generated file at once and must not churn because an unrelated edit reordered them.
    /// </summary>
    public bool SortGeneratedSourcesByHintName { get; init; }
}
