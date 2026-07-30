using SsalKit.Generators.Toolkit;

namespace SsalKit.StableHashing.Generator.Models;

/// <summary>
/// One <c>[StableHashContract]</c> type, as produced by <see cref="Parsing.ContractParser"/>:
/// everything needed to either emit its extension class or explain why nothing was emitted for
/// it, reduced to primitives so the incremental pipeline can compare two runs' models by value.
/// </summary>
/// <param name="TypeFqn">The contract type's <c>global::</c>-prefixed fully qualified name.</param>
/// <param name="TypeDisplayName">The contract type's short display name, for diagnostic messages.</param>
/// <param name="Namespace">
/// The contract type's namespace, or the empty string when it lives in the global namespace (in
/// which case no <c>namespace</c> block is emitted).
/// </param>
/// <param name="ExtensionClassName">
/// The generated extension class's name: the contract type's name, with the names of any
/// containing types flattened in front of it (<c>Outer_Inner</c>), plus the
/// <c>StableHashing</c> suffix (design §3.4).
/// </param>
/// <param name="HintName">The <c>AddSource</c> hint name for this type's generated file.</param>
/// <param name="IsClassContract">
/// Whether the contract type is a <see langword="class"/> (as opposed to a
/// <see langword="struct"/>/<see langword="record struct"/>), which is what decides whether the
/// generated <c>ComputeStableHash</c> null-checks its <c>value</c> parameter (design §3.4).
/// </param>
/// <param name="IsPublic">
/// Whether the generated extension class and its two methods should be declared
/// <see langword="public"/> (<see langword="true"/>) or <see langword="internal"/>
/// (<see langword="false"/>) -- <see langword="true"/> only when the contract type and every type
/// containing it are <see langword="public"/> (<see cref="SsalKit.Generators.Toolkit.SymbolFacts.IsEffectivelyPublic"/>).
/// A contract type that is merely <see langword="internal"/>/<c>protected internal</c> (rather
/// than inaccessible, which is SSALH007) still gets a fully usable, correctly-downgraded
/// <see langword="internal"/> extension class: declaring it <see langword="public"/> unconditionally
/// would fail to compile (CS0051, "inconsistent accessibility") the moment such a contract's own
/// type is only <see langword="internal"/>.
/// </param>
/// <param name="ContractName">
/// The contract's declared name (<c>StableHashContractAttribute.Name</c>), or
/// <see langword="null"/> when it was null/whitespace (SSALH009).
/// </param>
/// <param name="Version">The contract's declared version.</param>
/// <param name="NameDeclarationLocation">
/// Where the <c>[StableHashContract]</c> attribute is applied, used both for this type's own
/// diagnostics and for the cross-type SSALH011 (duplicate contract name) pass.
/// </param>
/// <param name="ReadyToEmit">
/// Whether this contract has no <see cref="Microsoft.CodeAnalysis.DiagnosticSeverity.Error"/>
/// diagnostic of its own. SSALH011 (duplicate contract name), added later by the cross-type pass,
/// is a warning and never changes this -- see <see cref="Parsing.ContractNameGrouper"/>.
/// </param>
/// <param name="Members">
/// The contract's successfully-validated members, in the order <see cref="Parsing.ContractParser"/>
/// encountered them (not yet id-sorted; <see cref="Emission.StableHashEmitter"/> sorts).
/// Empty when <paramref name="ReadyToEmit"/> is <see langword="false"/>, or when the contract
/// legitimately declares no members (SSALH010).
/// </param>
/// <param name="OwnDiagnostics">
/// This type's own diagnostics (type-level and member-level). Does not include SSALH011, which
/// can only be computed once every contract in the compilation has been collected.
/// </param>
internal sealed record ContractModel(
    string TypeFqn,
    string TypeDisplayName,
    string Namespace,
    string ExtensionClassName,
    string HintName,
    bool IsClassContract,
    bool IsPublic,
    string? ContractName,
    int Version,
    LocationInfo? NameDeclarationLocation,
    bool ReadyToEmit,
    EquatableArray<MemberModel> Members,
    EquatableArray<DiagnosticInfo> OwnDiagnostics);
