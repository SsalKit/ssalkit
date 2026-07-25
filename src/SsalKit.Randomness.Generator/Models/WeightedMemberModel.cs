using SsalKit.Randomness.Generator.Diagnostics;

namespace SsalKit.Randomness.Generator.Models;

/// <summary>
/// One <c>[RandomWeight]</c>-decorated member, as produced by the attribute transform: either the
/// emission model it resolved to, or the diagnostic that disqualified it. Exactly one of
/// <see cref="Type"/> and <see cref="Diagnostic"/> is non-<see langword="null"/>.
/// </summary>
/// <remarks>
/// The member survives the transform even when it is invalid, because a member that produced a
/// diagnostic still counts towards the "one weight member per type" rule (SSALR002), which can only
/// be evaluated after every member has been collected.
/// </remarks>
/// <param name="TypeFqn">
/// The declaring type's <c>global::</c>-prefixed fully qualified name, used to group members by
/// declaring type (partial declarations across files included).
/// </param>
/// <param name="TypeDisplayName">The declaring type's short display name, for diagnostic messages.</param>
/// <param name="MemberName">The member's own name, for diagnostic messages.</param>
/// <param name="Location">Where a diagnostic about this member is reported.</param>
/// <param name="Type">The emission model, when the member on its own is valid.</param>
/// <param name="Diagnostic">The member-level diagnostic, when it is not.</param>
internal sealed record WeightedMemberModel(
    string TypeFqn,
    string TypeDisplayName,
    string MemberName,
    LocationInfo? Location,
    WeightedTypeModel? Type,
    DiagnosticInfo? Diagnostic);
