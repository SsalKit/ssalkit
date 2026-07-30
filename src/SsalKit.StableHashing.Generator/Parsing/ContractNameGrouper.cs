using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SsalKit.Generators.Toolkit;
using SsalKit.StableHashing.Generator.Diagnostics;
using SsalKit.StableHashing.Generator.Models;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Folds every <see cref="ContractModel"/> collected across the compilation into the two things
/// the source-output stages consume: the contracts to emit, and every diagnostic to report.
/// </summary>
/// <remarks>
/// This is where SSALH011 (duplicate <c>[StableHashContract]</c> name) lives -- the one rule that
/// cannot be decided from a single contract type on its own, since it compares names across every
/// contract in the compilation. It is a warning, so unlike SSALH001-009/013 it never removes a
/// type from <see cref="ContractAnalysisResult.Types"/>: <see cref="ContractModel.ReadyToEmit"/>,
/// decided per-type in <see cref="ContractParser"/>, already reflects every rule that does block
/// emission.
/// </remarks>
internal static class ContractNameGrouper
{
    public static ContractAnalysisResult Analyze(ImmutableArray<ContractModel> models, CancellationToken cancellationToken)
    {
        if (models.IsDefaultOrEmpty)
        {
            return new ContractAnalysisResult(EquatableArray<ContractModel>.Empty, EquatableArray<DiagnosticInfo>.Empty);
        }

        var diagnostics = models.SelectMany(model => model.OwnDiagnostics).ToList();
        diagnostics.AddRange(FindDuplicateNameDiagnostics(models, cancellationToken));

        var types = models.Where(model => model.ReadyToEmit).ToImmutableArray();

        return new ContractAnalysisResult(
            EquatableArray.Create(types),
            EquatableArray.Create(SymbolFacts.SortForDiagnosticDeterminism(diagnostics.ToImmutableArray())));
    }

    private static System.Collections.Generic.IEnumerable<DiagnosticInfo> FindDuplicateNameDiagnostics(
        ImmutableArray<ContractModel> models, CancellationToken cancellationToken)
    {
        // Ordinal grouping/ordering by contract name: the order pipeline nodes happened to run in
        // must never leak into the reported diagnostic sequence (mirrors
        // SsalKit.Randomness.Generator's RandomWeightTypeGrouper).
        var groups = models
            .Where(model => model.ContractName is not null)
            .GroupBy(model => model.ContractName!, System.StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, System.StringComparer.Ordinal);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupList = group.OrderBy(model => model.TypeFqn, System.StringComparer.Ordinal).ToList();

            foreach (var model in groupList)
            {
                var others = string.Join(
                    ", ",
                    groupList
                        .Where(other => other.TypeFqn != model.TypeFqn)
                        .Select(other => "'" + other.TypeDisplayName + "'"));

                yield return new DiagnosticInfo(
                    DiagnosticDescriptors.DuplicateContractName, model.NameDeclarationLocation, model.TypeDisplayName, group.Key, others);
            }
        }
    }
}
