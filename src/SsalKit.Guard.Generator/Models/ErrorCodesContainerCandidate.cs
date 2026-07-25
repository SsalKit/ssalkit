using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Models;

/// <summary>
/// One <c>[ErrorCodes&lt;TCode&gt;]</c> application: the container's shape, its own
/// <c>[ExternalErrorCode&lt;TCode&gt;]</c> registrations, and whatever disqualified it. A container
/// carrying two <c>[ErrorCodes]</c> attributes with different code enums produces one candidate
/// each, and each one only sees the external registrations written for its own enum.
/// </summary>
/// <param name="IsValid">
/// Whether a file is generated for this container. <see langword="false"/> when SSALG002 or
/// SSALG007 fired, in which case only <see cref="Diagnostic"/> and <see cref="TCodeFqn"/> are
/// meaningful -- the enum is still read off the attribute so that a broken container still counts
/// as "a container exists for this enum" and does not also trigger SSALG008 on every exception.
/// </param>
/// <param name="TCodeFqn">The code enum's <c>global::</c>-prefixed name, the key exceptions join on.</param>
/// <param name="TCodeDisplayName">The code enum's name as documentation and diagnostics refer to it.</param>
/// <param name="TCodeIsEffectivelyPublic">
/// Whether the code enum and every type containing it are public, which decides whether the
/// generated <c>TryMap</c>/<c>MapOrDefault</c> may themselves be public: a public method may not
/// expose a less accessible type.
/// </param>
/// <param name="ContainerFqn">The container's <c>global::</c>-prefixed name, used to group and order containers.</param>
/// <param name="ContainerDisplayName">The container's name as documentation and diagnostics refer to it.</param>
/// <param name="Namespace">The container's namespace, or the empty string for the global namespace.</param>
/// <param name="ContainingTypeDeclarations">
/// The re-declaration of each type containing the container, outermost first, e.g.
/// <c>["public static partial class Outer"]</c>. Empty for a top-level container.
/// </param>
/// <param name="ContainerDeclaration">The container's own re-declaration, e.g. <c>public static partial class GameErrors</c>.</param>
/// <param name="HintName">The <c>AddSource</c> hint name for this container's generated file.</param>
/// <param name="ExternalRegistrations">The container's <c>[ExternalErrorCode]</c> registrations for this code enum.</param>
/// <param name="Location">Where to report a diagnostic about this container.</param>
/// <param name="Diagnostic">The diagnostic this container produced, if any.</param>
internal sealed record ErrorCodesContainerCandidate(
    bool IsValid,
    string TCodeFqn,
    string TCodeDisplayName,
    bool TCodeIsEffectivelyPublic,
    string ContainerFqn,
    string ContainerDisplayName,
    string Namespace,
    EquatableArray<string> ContainingTypeDeclarations,
    string ContainerDeclaration,
    string HintName,
    EquatableArray<ExternalRegistrationCandidate> ExternalRegistrations,
    LocationInfo? Location,
    DiagnosticInfo? Diagnostic);

/// <summary>
/// One <c>[ExternalErrorCode&lt;TCode&gt;]</c> registration on a container.
/// </summary>
/// <param name="IsValid">
/// Whether the registration takes part in the mapping table. <see langword="false"/> when SSALG004
/// fired, in which case only <see cref="Diagnostic"/> is meaningful and the rest of the container is
/// generated as if the registration had not been written.
/// </param>
/// <param name="ExceptionFqn">The registered type's <c>global::</c>-prefixed name.</param>
/// <param name="ExceptionDisplayName">The registered type's name as diagnostics refer to it.</param>
/// <param name="CodeExpression">The code, written as the generated code must write it.</param>
/// <param name="InheritanceDepth">The number of base-type steps from the registered type to <c>System.Exception</c>.</param>
/// <param name="Location">Where to report a diagnostic about this registration.</param>
/// <param name="Diagnostic">The diagnostic this registration produced, if any.</param>
internal sealed record ExternalRegistrationCandidate(
    bool IsValid,
    string ExceptionFqn,
    string ExceptionDisplayName,
    string CodeExpression,
    int InheritanceDepth,
    LocationInfo? Location,
    DiagnosticInfo? Diagnostic);
