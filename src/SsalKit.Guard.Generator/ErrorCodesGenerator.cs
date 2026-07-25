using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Guard.Generator.Emission;
using SsalKit.Guard.Generator.Models;
using SsalKit.Guard.Generator.Parsing;

namespace SsalKit.Guard.Generator;

/// <summary>
/// Generates the exception → error-code mapping table, and the per-code factory and throw helpers,
/// for every <see langword="static"/> <see langword="partial"/> class decorated with
/// <c>[SsalKit.Guard.ErrorCodes&lt;TCode&gt;]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two <c>ForAttributeWithMetadataName</c> sources feed the pipeline: the decorated exceptions
/// (<see cref="ErrorCodeAttributeMetadataName"/>) and the mapping containers
/// (<see cref="ErrorCodesAttributeMetadataName"/>). The containers' external registrations
/// (<see cref="ExternalErrorCodeAttributeMetadataName"/>) are read off the same container symbol
/// rather than through a third provider, since a provider of its own would only have to be joined
/// back to the container it was written on.
/// </para>
/// <para>
/// The two collections are combined and joined on the code enum: an exception takes part in a
/// container when their <c>TCode</c> match, which is what lets several code enums coexist in one
/// assembly, and an exception whose <c>TCode</c> has no container at all is reported (SSALG008)
/// rather than silently dropped. Each container's entries are then ordered by inheritance depth
/// descending, ties broken by fully qualified name — the derived-before-base guarantee, with a
/// tiebreak that keeps the emitted file deterministic.
/// </para>
/// <para>
/// The analysis result is split into two projections — models and diagnostics — so an edit that
/// only changes a diagnostic leaves every generated file untouched, and vice versa. Models are
/// records over <c>EquatableArray&lt;T&gt;</c>, diagnostics travel as the toolkit's cache-safe
/// <c>DiagnosticInfo</c>/<c>LocationInfo</c>, emission uses <c>IndentedCodeWriter</c>, hint names go
/// through <c>HintNameSanitizer</c>, and the descriptor table is built from
/// <c>DiagnosticDescriptorFactory("SSALG", "SsalKit.Guard")</c>.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ErrorCodesGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The metadata name of <c>SsalKit.Guard.ErrorCodeAttribute&lt;TCode&gt;</c>, which declares an
    /// exception type's code.
    /// </summary>
    /// <remarks>
    /// These are generic attributes, so the name carries the CLR arity suffix
    /// (<c>`1</c>) — <c>ForAttributeWithMetadataName</c> matches on the metadata name and will
    /// silently find nothing if the suffix is omitted.
    /// </remarks>
    internal const string ErrorCodeAttributeMetadataName = "SsalKit.Guard.ErrorCodeAttribute`1";

    /// <summary>
    /// The metadata name of <c>SsalKit.Guard.ErrorCodesAttribute&lt;TCode&gt;</c>, which marks a
    /// mapping container.
    /// </summary>
    internal const string ErrorCodesAttributeMetadataName = "SsalKit.Guard.ErrorCodesAttribute`1";

    /// <summary>
    /// The metadata name of <c>SsalKit.Guard.ExternalErrorCodeAttribute&lt;TCode&gt;</c>, which
    /// registers an exception type the consumer does not own.
    /// </summary>
    internal const string ExternalErrorCodeAttributeMetadataName = "SsalKit.Guard.ExternalErrorCodeAttribute`1";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<EquatableArray<ErrorCodeExceptionCandidate>> exceptions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ErrorCodeAttributeMetadataName,
                // Deliberately unfiltered, as in the other SsalKit generators: [AttributeUsage(Class)]
                // already restricts what can carry the attribute, and the symbol-kind check in the
                // parser covers the same ground without having to enumerate node kinds here.
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => ErrorCodeExceptionParser.GetCandidates(ctx, ct))
            .WithTrackingName(TrackingNames.Exceptions);

        IncrementalValuesProvider<EquatableArray<ErrorCodesContainerCandidate>> containers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ErrorCodesAttributeMetadataName,
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => ErrorCodesContainerParser.GetCandidates(ctx, ct))
            .WithTrackingName(TrackingNames.Containers);

        IncrementalValueProvider<ImmutableArray<EquatableArray<ErrorCodeExceptionCandidate>>> collectedExceptions =
            exceptions.Collect().WithTrackingName(TrackingNames.CollectedExceptions);

        IncrementalValueProvider<ImmutableArray<EquatableArray<ErrorCodesContainerCandidate>>> collectedContainers =
            containers.Collect().WithTrackingName(TrackingNames.CollectedContainers);

        // The join has to see both sides at once: which container an exception belongs to, whether a
        // registration is a duplicate, and whether a code enum has a container at all are all
        // questions about the whole compilation, not about one declaration.
        IncrementalValueProvider<ErrorCodesAnalysisResult> analysis = collectedContainers
            .Combine(collectedExceptions)
            .Select(static (pair, ct) => ErrorCodesAssembler.Analyze(pair.Left, pair.Right, ct))
            .WithTrackingName(TrackingNames.Analysis);

        // Two projections off that one node, so emission and diagnostics cache independently.
        IncrementalValueProvider<EquatableArray<ErrorCodesContainerModel>> models = analysis
            .Select(static (result, _) => result.Containers)
            .WithTrackingName(TrackingNames.Models);

        IncrementalValueProvider<EquatableArray<DiagnosticInfo>> diagnostics = analysis
            .Select(static (result, _) => result.Diagnostics)
            .WithTrackingName(TrackingNames.Diagnostics);

        context.RegisterSourceOutput(diagnostics, static (spc, reported) =>
        {
            foreach (var diagnostic in reported)
            {
                spc.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
        });

        context.RegisterSourceOutput(models, static (spc, containerModels) =>
        {
            foreach (var model in containerModels)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.AddSource(model.HintName, ErrorCodesEmitter.Emit(model));
            }
        });
    }

    /// <summary>
    /// Names assigned to each pipeline stage via <c>WithTrackingName</c>, so tests can inspect
    /// <see cref="IncrementalGeneratorRunStep"/> results (via
    /// <c>GeneratorDriverRunResult.Results[i].TrackedSteps</c>) to confirm the pipeline caches
    /// correctly across runs that don't change its inputs.
    /// </summary>
    internal static class TrackingNames
    {
        public const string Exceptions = "Exceptions";
        public const string Containers = "Containers";
        public const string CollectedExceptions = "CollectedExceptions";
        public const string CollectedContainers = "CollectedContainers";
        public const string Analysis = "Analysis";
        public const string Models = "Models";
        public const string Diagnostics = "Diagnostics";
    }
}
