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
/// The weight member is found through two pipeline branches. An attribute on a property or field is
/// collected by <c>ForAttributeWithMetadataName</c>; one written with an attribute target specifier
/// -- <c>[property: RandomWeight]</c> on a positional record parameter, or <c>[field:
/// RandomWeight]</c> -- attaches to a symbol that provider structurally cannot report, and is
/// collected by a second, syntax-driven branch instead (see
/// <see cref="TargetRedirectedRandomWeightParser"/>). Both branches produce the same member models
/// and are concatenated before grouping, so every rule -- SSALR002 included -- sees the whole set.
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

        IncrementalValueProvider<ImmutableArray<WeightedMemberModel>> collected = Collect(members)
            .WithTrackingName(TrackingNames.CollectedMembers);

        // The second branch: attribute applications the provider above cannot see because a target
        // specifier moved them onto another symbol. It is syntax-driven, so its predicate visits
        // every node of every changed tree -- which is why it is kept as narrow as possible and why
        // the ordinary property/field case still goes through the attribute provider.
        IncrementalValuesProvider<WeightedMemberModel?> redirectedMembers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => TargetRedirectedRandomWeightParser.IsCandidate(node),
                transform: static (ctx, ct) => TargetRedirectedRandomWeightParser.GetModel(ctx, ct))
            .WithTrackingName(TrackingNames.RedirectedMembers);

        IncrementalValueProvider<ImmutableArray<WeightedMemberModel>> collectedRedirected = Collect(redirectedMembers)
            .WithTrackingName(TrackingNames.CollectedRedirectedMembers);

        // Grouping by declaring type happens once, here, over both branches at once: SSALR002 ("one
        // weight member per type") is the one rule that cannot be decided while looking at a single
        // member, and a type can well declare one member through each branch.
        IncrementalValueProvider<RandomWeightAnalysisResult> analysis = collected
            .Combine(collectedRedirected)
            .Select(static (branches, ct) =>
                RandomWeightTypeGrouper.Analyze(Concat(branches.Left, branches.Right), ct))
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
    /// Drops the members a transform declined to model and batches the rest.
    /// </summary>
    private static IncrementalValueProvider<ImmutableArray<WeightedMemberModel>> Collect(
        IncrementalValuesProvider<WeightedMemberModel?> members)
    {
        IncrementalValuesProvider<WeightedMemberModel> present = members.Where(static model => model is not null)!;

        return present.Collect();
    }

    /// <summary>
    /// Joins the two branches' members, without allocating when either branch found nothing -- which
    /// is the common case for the redirected branch.
    /// </summary>
    private static ImmutableArray<WeightedMemberModel> Concat(
        ImmutableArray<WeightedMemberModel> members, ImmutableArray<WeightedMemberModel> redirected)
    {
        if (redirected.IsDefaultOrEmpty)
        {
            return members;
        }

        return members.IsDefaultOrEmpty ? redirected : members.AddRange(redirected);
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
        public const string RedirectedMembers = "RedirectedMembers";
        public const string CollectedRedirectedMembers = "CollectedRedirectedMembers";
        public const string Analysis = "Analysis";
        public const string Types = "Types";
        public const string Diagnostics = "Diagnostics";
    }
}
