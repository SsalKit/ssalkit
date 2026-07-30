using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
/// This is where two rules live that cannot be decided from a single contract type on its own,
/// since both compare across every contract in the compilation:
/// <list type="bullet">
/// <item><description>
/// SSALH011 (duplicate <c>[StableHashContract]</c> name) is a warning, so unlike SSALH001-009/013
/// it never removes a type from <see cref="ContractAnalysisResult.Types"/>:
/// <see cref="ContractModel.ReadyToEmit"/>, decided per-type in <see cref="ContractParser"/>,
/// already reflects every rule that does block emission.
/// </description></item>
/// <item><description>
/// Extension-class-name disambiguation (<see cref="DisambiguateExtensionClassNames"/>) resolves
/// the case where two *different* contract types happen to flatten to the same generated class
/// name -- most commonly a nested <c>Outer.Inner</c> contract colliding with an unrelated
/// top-level <c>Outer_Inner</c> contract in the same namespace, since
/// <see cref="ContractNaming.BuildExtensionClassName"/> flattens the former to
/// <c>Outer_InnerStableHashing</c>, identically to the latter. Without this pass both would emit a
/// class of the same name in the same namespace (CS0101).
/// </description></item>
/// </list>
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

        var disambiguated = DisambiguateExtensionClassNames(models);

        var types = disambiguated.Where(model => model.ReadyToEmit).ToImmutableArray();

        return new ContractAnalysisResult(
            EquatableArray.Create(types),
            EquatableArray.Create(SymbolFacts.SortForDiagnosticDeterminism(diagnostics.ToImmutableArray())));
    }

    /// <summary>
    /// Gives every contract's generated extension class a name that is unique within its
    /// namespace, so two contract types whose flattened names coincide cannot emit two classes
    /// with the same name (CS0101).
    /// </summary>
    /// <remarks>
    /// Ported from <c>SsalKit.Randomness.Generator</c>'s
    /// <c>RandomWeightTypeGrouper.DisambiguateExtensionClassNames</c>, which faces the exact same
    /// problem for the exact same reason (flattening a nesting chain is not injective). The winner
    /// is decided by <see cref="ContractModel.TypeFqn"/> in ordinal order -- computed here, not
    /// inherited from <paramref name="models"/>'s incoming order -- so which contract keeps the
    /// unsuffixed name never depends on the order pipeline nodes happened to run in, and adding an
    /// unrelated third contract elsewhere in the compilation cannot rename either of them.
    /// </remarks>
    private static ImmutableArray<ContractModel> DisambiguateExtensionClassNames(ImmutableArray<ContractModel> models)
    {
        // Overwhelmingly the common case: nothing to rename, and nothing allocated to find that out.
        if (models.Length < 2)
        {
            return models;
        }

        var ordered = models.OrderBy(static model => model.TypeFqn, System.StringComparer.Ordinal).ToImmutableArray();

        var taken = new HashSet<string>(System.StringComparer.Ordinal);
        var renamed = ImmutableArray.CreateBuilder<ContractModel>(ordered.Length);

        foreach (var model in ordered)
        {
            var key = model.Namespace + "::" + model.ExtensionClassName;
            if (taken.Add(key))
            {
                renamed.Add(model);
                continue;
            }

            var suffix = 2;
            string candidateName;
            string candidateKey;
            do
            {
                candidateName = model.ExtensionClassName + suffix.ToString(CultureInfo.InvariantCulture);
                candidateKey = model.Namespace + "::" + candidateName;
                suffix++;
            }
            while (!taken.Add(candidateKey));

            renamed.Add(model with { ExtensionClassName = candidateName });
        }

        return renamed.ToImmutable();
    }

    private static IEnumerable<DiagnosticInfo> FindDuplicateNameDiagnostics(
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
