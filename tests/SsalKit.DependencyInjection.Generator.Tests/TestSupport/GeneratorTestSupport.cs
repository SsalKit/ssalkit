using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Analysis;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests.TestSupport;

/// <summary>
/// The few facts about <em>this</em> package that every test in this project shares: which
/// assemblies the generated code is type-checked against, which diagnostic prefix belongs to it,
/// and which generator and analyzers are under test. Everything else comes from
/// <see cref="GeneratorTest"/>.
/// </summary>
internal static class GeneratorTestSupport
{
    /// <summary>
    /// The default options: the real
    /// <see cref="SsalKit.DependencyInjection.ServiceAttribute"/>/<c>RegistrationMode</c> types and,
    /// transitively, <c>IServiceCollection</c>/<c>ServiceLifetime</c>/<c>ServiceDescriptor</c> and
    /// the keyed-service extensions, so generated registrations type-check against the shipping API.
    /// Only <c>SSAL</c> diagnostics are surfaced, so a deliberately invalid test source's incidental
    /// compiler diagnostics never reach an assertion.
    /// </summary>
    public static readonly GeneratorTestOptions Options = new()
    {
        DiagnosticIdPrefix = "SSAL",
        AdditionalAssemblies =
        [
            typeof(SsalKit.DependencyInjection.ServiceAttribute).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.ServiceLifetime).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions).Assembly,
        ],
    };

    /// <summary>
    /// The options for the tests that pin the assembly name, because the generator derives the
    /// emitted file and extension-class names from it.
    /// </summary>
    public static readonly GeneratorTestOptions SampleAssembly = Options with { AssemblyName = "SsalKit.Sample" };

    /// <summary>
    /// The options for the tests whose source declares <c>unsafe</c> members.
    /// </summary>
    public static readonly GeneratorTestOptions Unsafe = Options with { AllowUnsafe = true };

    /// <summary>
    /// The options for a test that needs a second, separately compiled assembly in the compilation.
    /// </summary>
    public static GeneratorTestOptions Referencing(params MetadataReference[] references) =>
        Options with { AdditionalReferences = [.. references] };

    /// <summary>
    /// Runs the real <see cref="ServiceRegistrationGenerator"/> over <paramref name="source"/>.
    /// </summary>
    public static GeneratorTestResult RunGenerator(string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.Run<ServiceRegistrationGenerator>(source, options ?? Options);

    /// <summary>
    /// Runs every analyzer together, exactly as they run when the package is consumed: whichever
    /// attribute a test source uses, the others must stay silent about it.
    /// </summary>
    public static Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source, GeneratorTestOptions? options = null) =>
        GeneratorTest.RunAnalyzersAsync(
            source,
            [new ServiceAttributeAnalyzer(), new ServiceFactoryAnalyzer(), new RegisterImplementationsOfAnalyzer()],
            options ?? Options);
}
