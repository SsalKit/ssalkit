using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
            EquatableArray.Create(DisambiguateExtensionClassNames(types.ToImmutable())),
            EquatableArray.Create(SymbolFacts.SortForDiagnosticDeterminism(diagnostics.ToImmutable())));
    }

    /// <summary>
    /// Gives every generated class a name that is unique within its namespace, so two types whose
    /// flattened names coincide cannot emit two classes with the same name (CS0101).
    /// </summary>
    /// <remarks>
    /// The flattening that turns a nested <c>Outer.Inner</c> into <c>Outer_Inner</c> is not
    /// injective: a sibling top-level type literally named <c>Outer_Inner</c> flattens to the same
    /// string, and both would then want <c>Outer_InnerRandomWeightExtensions</c> in the same
    /// namespace. The hint names already differ (they are built from the fully qualified name, which
    /// keeps the two apart), so the collision only ever shows up as a duplicate type name in the
    /// consumer's compilation. Rather than reject one of the two types, the second and any further
    /// claimant gets a numeric suffix. The winner is decided by fully qualified name in ordinal
    /// order -- <paramref name="types"/> is already in that order -- so which type keeps the
    /// unsuffixed name never depends on the order pipeline nodes happened to run in, and adding an
    /// unrelated third type elsewhere in the compilation cannot rename either of them.
    /// </remarks>
    private static ImmutableArray<WeightedTypeModel> DisambiguateExtensionClassNames(
        ImmutableArray<WeightedTypeModel> types)
    {
        // Overwhelmingly the common case: nothing to rename, and nothing allocated to find that out.
        if (types.Length < 2)
        {
            return types;
        }

        var taken = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<WeightedTypeModel>.Builder? renamed = null;

        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var key = type.Namespace + "::" + type.ExtensionClassName;
            if (taken.Add(key))
            {
                renamed?.Add(type);
                continue;
            }

            renamed ??= ImmutableArray.CreateBuilder<WeightedTypeModel>(types.Length);
            if (renamed.Count == 0)
            {
                renamed.AddRange(types, i);
            }

            var suffix = 2;
            string candidate;
            do
            {
                candidate = type.ExtensionClassName + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            while (!taken.Add(type.Namespace + "::" + candidate));

            renamed.Add(type with { ExtensionClassName = candidate });
        }

        return renamed is null ? types : renamed.ToImmutable();
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
}
