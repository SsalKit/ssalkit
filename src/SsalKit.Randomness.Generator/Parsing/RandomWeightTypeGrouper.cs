using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SsalKit.Generators.Toolkit;
using SsalKit.Randomness.Generator.Diagnostics;
using SsalKit.Randomness.Generator.Models;

namespace SsalKit.Randomness.Generator.Parsing;

/// <summary>
/// Folds the per-member models produced by <see cref="RandomWeightMemberParser"/> into the two
/// things the source-output stages consume: the types to emit, and the diagnostics to report.
/// </summary>
/// <remarks>
/// This is where the one rule that cannot be decided per member lives -- SSALR002, "a type may
/// declare only one <c>[RandomWeight]</c> member" -- and where the all-or-nothing rule is applied:
/// a type with any diagnostic gets no extension class at all, so a consumer never ends up with a
/// half-generated API on top of an error they have to fix anyway.
/// </remarks>
internal static class RandomWeightTypeGrouper
{
    public static RandomWeightAnalysisResult Analyze(
        ImmutableArray<WeightedMemberModel> members, CancellationToken cancellationToken)
    {
        if (members.IsDefaultOrEmpty)
        {
            return new RandomWeightAnalysisResult(
                EquatableArray<WeightedTypeModel>.Empty, EquatableArray<DiagnosticInfo>.Empty);
        }

        var types = ImmutableArray.CreateBuilder<WeightedTypeModel>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        // Ordinal grouping/ordering by fully qualified name: the order pipeline nodes happened to
        // run in must never leak into the generated output or the diagnostic list.
        var groups = members
            .GroupBy(member => member.TypeFqn, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupMembers = group.ToList();

            foreach (var member in groupMembers)
            {
                if (member.Diagnostic is not null)
                {
                    diagnostics.Add(member.Diagnostic);
                }
            }

            if (groupMembers.Count > 1)
            {
                AddDuplicateMemberDiagnostics(groupMembers, diagnostics);
                continue;
            }

            var single = groupMembers[0];
            if (single.Type is not null)
            {
                types.Add(single.Type);
            }
        }

        return new RandomWeightAnalysisResult(
            EquatableArray.Create(types.ToImmutable()),
            EquatableArray.Create(SortForDeterminism(diagnostics.ToImmutable())));
    }

    /// <summary>
    /// Reports SSALR002 on every decorated member of the offending type rather than only on the
    /// second one: which member to keep is the user's decision, and highlighting all of them shows
    /// the full set to choose from -- including declarations in other files of a partial type.
    /// </summary>
    private static void AddDuplicateMemberDiagnostics(
        List<WeightedMemberModel> groupMembers, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var memberList = string.Join(
            ", ",
            groupMembers.Select(member => "'" + member.MemberName + "'").OrderBy(name => name, StringComparer.Ordinal));

        foreach (var member in groupMembers)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.DuplicateWeightMember, member.Location, member.TypeDisplayName, memberList));
        }
    }

    /// <summary>
    /// Orders diagnostics by source position (then id) so a run's diagnostic sequence is a function
    /// of the compilation alone. Diagnostics without a source location sort last (<c>false</c>
    /// orders before <c>true</c>).
    /// </summary>
    private static ImmutableArray<DiagnosticInfo> SortForDeterminism(ImmutableArray<DiagnosticInfo> diagnostics) =>
        diagnostics
            .OrderBy(diagnostic => diagnostic.Location is null)
            .ThenBy(diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location?.TextSpan.Start ?? 0)
            .ThenBy(diagnostic => diagnostic.Descriptor.Id, StringComparer.Ordinal)
            .ToImmutableArray();
}
