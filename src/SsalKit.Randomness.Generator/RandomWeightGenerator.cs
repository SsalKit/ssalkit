using Microsoft.CodeAnalysis;

namespace SsalKit.Randomness.Generator;

/// <summary>
/// Generates selector-less weighted-picking extension methods for every type that declares a
/// member decorated with <c>[SsalKit.Randomness.RandomWeight]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Scaffold stage: the generator is registered and loads as a Roslyn component, but
/// <see cref="Initialize"/> registers no pipeline yet, so it contributes no source and no
/// diagnostics. That keeps the packaging wiring (this assembly ships inside the
/// <c>SsalKit.Randomness</c> package under <c>analyzers/dotnet/cs</c>) verifiable on its own,
/// before any generation behaviour exists to confuse a failure with.
/// </para>
/// <para>
/// The pipeline lands next, following the SsalKit.DependencyInjection generator's layout —
/// <c>Models/</c> for the equatable pipeline models, <c>Parsing/</c> for the
/// <c>ForAttributeWithMetadataName("SsalKit.Randomness.RandomWeightAttribute")</c> transform that
/// turns a decorated member into a model or a diagnostic, <c>Emission/</c> for the extension-class
/// writer, and <c>Diagnostics/</c> for the SSALR descriptor table. Those folders do not exist yet
/// and are created with their first file.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class RandomWeightGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The metadata name of the marker attribute that drives generation. Kept here so the parsing
    /// stage and its tests share one definition of the string the pipeline keys on.
    /// </summary>
    internal const string RandomWeightAttributeMetadataName = "SsalKit.Randomness.RandomWeightAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Intentionally empty at the scaffold stage -- see the remarks on the type. Registering a
        // no-op source output here instead would emit an empty file into every consuming
        // compilation, which is worse than contributing nothing.
        _ = context;
    }
}
