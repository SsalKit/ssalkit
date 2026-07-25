using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Models;

/// <summary>
/// One <c>[ErrorCode&lt;TCode&gt;]</c> application, reduced to the strings the assembler and the
/// emitter need. A type carrying two <c>[ErrorCode]</c> attributes with different code enums
/// produces one candidate each.
/// </summary>
/// <remarks>
/// No <c>ISymbol</c>, <c>SyntaxNode</c> or <c>Compilation</c> survives into this record -- only
/// strings, enums and <see cref="LocationInfo"/> -- which is what lets the incremental pipeline
/// compare two runs by value instead of re-emitting on every keystroke.
/// </remarks>
/// <param name="IsValid">
/// Whether the candidate takes part in a container. <see langword="false"/> when a rule that
/// disqualifies the registration fired (SSALG001, SSALG005), in which case only
/// <see cref="Diagnostic"/> is meaningful.
/// </param>
/// <param name="TCodeFqn">The code enum's <c>global::</c>-prefixed name, the key a container joins on.</param>
/// <param name="TCodeDisplayName">The code enum's name as written in diagnostics.</param>
/// <param name="ExceptionFqn">The exception type's <c>global::</c>-prefixed name.</param>
/// <param name="ExceptionDisplayName">The exception type's name as written in diagnostics.</param>
/// <param name="ExceptionName">
/// The exception type's own name, e.g. <c>UserNotFoundException</c>, which the helper name is
/// derived from.
/// </param>
/// <param name="ExceptionFlattenedName">
/// The exception's fully qualified name flattened into a single identifier
/// (<c>Game_Loot_UserNotFoundException</c>), used as the last-resort helper name when two
/// registrations in one container would otherwise produce the same one.
/// </param>
/// <param name="CodeExpression">The code, written as the generated code must write it.</param>
/// <param name="CodeDisplayName">The code as documentation refers to it, e.g. <c>GameStatusCode.UserNotFound</c>.</param>
/// <param name="InheritanceDepth">
/// The number of base-type steps from the exception to <c>System.Exception</c>. The mapping table's
/// primary sort key, descending, which is the derived-before-base guarantee.
/// </param>
/// <param name="Constructor">The widest recognised constructor shape, or <see cref="ConstructorShape.None"/>.</param>
/// <param name="MessageIsNullable">Whether the mirrored constructor's message parameter accepts <see langword="null"/>.</param>
/// <param name="InnerIsNullable">Whether the mirrored constructor's inner-exception parameter accepts <see langword="null"/>.</param>
/// <param name="IsEffectivelyPublic">
/// Whether the exception type and every type containing it are public, which decides whether the
/// generated helpers may themselves be public.
/// </param>
/// <param name="Location">Where to report a diagnostic about this candidate.</param>
/// <param name="Diagnostic">The diagnostic this candidate produced, if any.</param>
internal sealed record ErrorCodeExceptionCandidate(
    bool IsValid,
    string TCodeFqn,
    string TCodeDisplayName,
    string ExceptionFqn,
    string ExceptionDisplayName,
    string ExceptionName,
    string ExceptionFlattenedName,
    string CodeExpression,
    string CodeDisplayName,
    int InheritanceDepth,
    ConstructorShape Constructor,
    bool MessageIsNullable,
    bool InnerIsNullable,
    bool IsEffectivelyPublic,
    LocationInfo? Location,
    DiagnosticInfo? Diagnostic)
{
    /// <summary>
    /// Creates the candidate for an application that broke a rule: it carries the diagnostic and
    /// nothing else, and never reaches a container.
    /// </summary>
    public static ErrorCodeExceptionCandidate Invalid(LocationInfo? location, DiagnosticInfo diagnostic) =>
        new(
            IsValid: false,
            TCodeFqn: string.Empty,
            TCodeDisplayName: string.Empty,
            ExceptionFqn: string.Empty,
            ExceptionDisplayName: string.Empty,
            ExceptionName: string.Empty,
            ExceptionFlattenedName: string.Empty,
            CodeExpression: string.Empty,
            CodeDisplayName: string.Empty,
            InheritanceDepth: 0,
            Constructor: ConstructorShape.None,
            MessageIsNullable: true,
            InnerIsNullable: true,
            IsEffectivelyPublic: false,
            Location: location,
            Diagnostic: diagnostic);
}
