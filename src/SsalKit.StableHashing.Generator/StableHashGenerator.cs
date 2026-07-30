using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.StableHashing.Generator.Emission;
using SsalKit.StableHashing.Generator.Models;
using SsalKit.StableHashing.Generator.Parsing;

namespace SsalKit.StableHashing.Generator;

/// <summary>
/// Generates <c>ComputeStableHash</c>/<c>AppendStableHash</c> extension methods for every type
/// decorated with <c>[SsalKit.StableHashing.StableHashContract]</c> (design §3.4).
/// </summary>
/// <remarks>
/// <para>
/// Two independent <c>ForAttributeWithMetadataName</c> branches feed this generator. The
/// <c>[StableHashContract]</c> branch does the real work: one <see cref="ContractParser.Parse"/>
/// call per contract type produces every type-level and member-level diagnostic together with the
/// members ready to emit -- <see cref="ITypeSymbol.GetMembers()"/> already returns a type's full,
/// partial-declarations-merged member list in a single call, so unlike
/// <c>SsalKit.Randomness.Generator</c>'s per-member pipeline there is no need to collect and
/// re-group members by declaring type before every member of a type can be seen together. A
/// second, much smaller <c>[StableHashMember]</c> branch exists solely to report SSALH012 (an
/// orphaned member attribute on a type that never got <c>[StableHashContract]</c> at all) --
/// exactly the one case the first branch can never see, because it is never invoked for such a
/// type.
/// </para>
/// <para>
/// A single collect-then-group stage (<see cref="ContractNameGrouper"/>) sits after the contract
/// branch, purely to compute SSALH011 (duplicate contract name across the compilation) -- the one
/// rule that cannot be decided from a single contract type in isolation. Everything else
/// (SSALH001-002-003-004-005-006-007-008-009-010-013) is decided entirely within
/// <see cref="ContractParser.Parse"/>, per type.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class StableHashGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<DiagnosticInfo?> orphanDiagnostics = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ContractAttributeInfo.MemberAttributeMetadataName,
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => OrphanMemberParser.GetOrphanDiagnostic(ctx, ct));

        context.RegisterSourceOutput(
            orphanDiagnostics.Where(static diagnostic => diagnostic is not null)!,
            static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic!.ToDiagnostic()));

        IncrementalValuesProvider<ContractModel> perType = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ContractAttributeInfo.ContractAttributeMetadataName,
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => ContractParser.Parse(ctx, ct))
            .WithTrackingName(TrackingNames.PerType);

        IncrementalValueProvider<ImmutableArray<ContractModel>> collected = perType
            .Collect()
            .WithTrackingName(TrackingNames.Collected);

        // The one rule that cannot be decided while looking at a single contract type: SSALH011
        // ("duplicate contract name in the compilation").
        IncrementalValueProvider<ContractAnalysisResult> analysis = collected
            .Select(static (models, ct) => ContractNameGrouper.Analyze(models, ct))
            .WithTrackingName(TrackingNames.Analysis);

        // Two projections off that one node, so emission and diagnostics cache independently: an
        // edit that only adds a diagnostic (e.g. a second contract taking an existing name) leaves
        // every generated file untouched, and vice versa.
        IncrementalValueProvider<EquatableArray<ContractModel>> types = analysis
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
                spc.AddSource(model.HintName, StableHashEmitter.Emit(model));
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
        public const string PerType = "PerType";
        public const string Collected = "Collected";
        public const string Analysis = "Analysis";
        public const string Types = "Types";
        public const string Diagnostics = "Diagnostics";
    }
}
