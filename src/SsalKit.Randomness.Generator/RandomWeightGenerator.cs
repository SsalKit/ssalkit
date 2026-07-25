using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Randomness.Generator.Diagnostics;
using SsalKit.Randomness.Generator.Emission;
using SsalKit.Randomness.Generator.Models;
using SsalKit.Randomness.Generator.Parsing;

namespace SsalKit.Randomness.Generator;

/// <summary>
/// Generates selector-less weighted-picking extension methods for every type that declares a
/// member decorated with <c>[SsalKit.Randomness.RandomWeight]</c>.
/// </summary>
/// <remarks>
/// <para>
/// One static extension class is emitted per decorated type, into that type's own namespace, so
/// <c>lootTable.PickWeighted(random)</c> compiles with no extra <c>using</c> and no reflection: the
/// generated methods are ordinary C# that delegates to the selector-based runtime overloads on
/// <c>WeightedRandomExtensions</c>, with the selector written for the consumer at compile time.
/// </para>
/// <para>
/// Which methods a type gets depends on its weight member's type: an integral member yields
/// <c>PickWeighted</c>, <c>PickManyWeighted</c>, <c>PickManyWeightedDistinct</c>, and
/// <c>ToWeightedSampler</c>; a floating-point member yields <c>PickWeighted</c> alone, mirroring the
/// runtime surface. Anything the generator cannot honour is reported as an <c>SSALR</c> error
/// (see <see cref="DiagnosticDescriptors"/>) rather than silently skipped, and a type with any
/// diagnostic gets no extension class at all.
/// </para>
/// <para>
/// Every generated method takes the random source explicitly. A type that declares
/// <c>[RandomWeight(SharedSourceOverloads = true)]</c> additionally gets argument-less overloads
/// that delegate to those, passing <c>SharedRandomSource.Instance</c>.
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
        IncrementalValuesProvider<WeightedMemberModel?> members = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RandomWeightAttributeMetadataName,
                // Deliberately unfiltered. [AttributeUsage(Property | Field)] already restricts what
                // can carry the attribute, and the shapes that reach here are spread over unrelated
                // node kinds -- PropertyDeclarationSyntax, IndexerDeclarationSyntax (rejected, but
                // with a diagnostic), EnumMemberDeclarationSyntax, and VariableDeclaratorSyntax for
                // fields. Enumerating them here would silently drop whichever one was forgotten;
                // the symbol-kind check in the parser covers the same ground without that failure
                // mode, and the attribute-name match ForAttributeWithMetadataName already performed
                // keeps the candidate set tiny either way.
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => RandomWeightMemberParser.GetModel(ctx, ct))
            .WithTrackingName(TrackingNames.Members);

        IncrementalValuesProvider<WeightedMemberModel> validMembers = members
            .Where(static model => model is not null)!;

        IncrementalValueProvider<ImmutableArray<WeightedMemberModel>> collected = validMembers
            .Collect()
            .WithTrackingName(TrackingNames.CollectedMembers);

        // Grouping by declaring type happens once, here: SSALR002 ("one weight member per type")
        // is the one rule that cannot be decided while looking at a single member.
        IncrementalValueProvider<RandomWeightAnalysisResult> analysis = collected
            .Select(static (models, ct) => RandomWeightTypeGrouper.Analyze(models, ct))
            .WithTrackingName(TrackingNames.Analysis);

        // Two projections off that one node, so emission and diagnostics cache independently: an
        // edit that only adds a diagnostic leaves every generated file untouched, and vice versa.
        IncrementalValueProvider<EquatableArray<WeightedTypeModel>> types = analysis
            .Select(static (result, _) => result.Types)
            .WithTrackingName(TrackingNames.Types);

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

        context.RegisterSourceOutput(types, static (spc, models) =>
        {
            foreach (var model in models)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.AddSource(model.HintName, RandomWeightExtensionsEmitter.Emit(model));
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
        public const string Members = "Members";
        public const string CollectedMembers = "CollectedMembers";
        public const string Analysis = "Analysis";
        public const string Types = "Types";
        public const string Diagnostics = "Diagnostics";
    }
}
