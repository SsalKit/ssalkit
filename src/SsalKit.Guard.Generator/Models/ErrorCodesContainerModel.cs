using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Models;

/// <summary>
/// Everything the emitter needs to write one container's generated part, reduced to primitives so
/// the incremental pipeline can compare two runs' models by value.
/// </summary>
/// <param name="Namespace">The container's namespace, or the empty string when no <c>namespace</c> block is emitted.</param>
/// <param name="ContainingTypeDeclarations">The re-declaration of each containing type, outermost first.</param>
/// <param name="ContainerDeclaration">The container's own re-declaration.</param>
/// <param name="TCodeFqn">The code enum's <c>global::</c>-prefixed name.</param>
/// <param name="TCodeDisplayName">The code enum's name, for documentation.</param>
/// <param name="MapAccessibility">
/// The accessibility keyword of <c>TryMap</c> and <c>MapOrDefault</c>: <c>public</c>, unless the
/// code enum is not itself public, in which case a public signature would not compile.
/// </param>
/// <param name="Entries">
/// The mapping table, already ordered most-derived first (inheritance depth descending, ties broken
/// by fully qualified name).
/// </param>
/// <param name="Helpers">
/// The factory and throw helpers to write, ordered by exception name. Only exceptions declared in
/// this compilation with <c>[ErrorCode]</c> and a recognised constructor appear here.
/// </param>
/// <param name="HintName">The <c>AddSource</c> hint name for this container's generated file.</param>
internal sealed record ErrorCodesContainerModel(
    string Namespace,
    EquatableArray<string> ContainingTypeDeclarations,
    string ContainerDeclaration,
    string TCodeFqn,
    string TCodeDisplayName,
    string MapAccessibility,
    EquatableArray<MappingEntryModel> Entries,
    EquatableArray<ExceptionHelperModel> Helpers,
    string HintName);

/// <summary>
/// One row of the generated mapping table.
/// </summary>
/// <param name="ExceptionFqn">The type the generated lookup tests for.</param>
/// <param name="CodeExpression">The code assigned when that test succeeds.</param>
internal sealed record MappingEntryModel(string ExceptionFqn, string CodeExpression);

/// <summary>
/// One exception's generated factory and throw helper.
/// </summary>
/// <param name="ExceptionFqn">The exception type the helpers construct.</param>
/// <param name="FactoryName">The factory's name, already escaped if it collides with a keyword.</param>
/// <param name="ThrowName">The throw helper's name, already escaped if it collides with a keyword.</param>
/// <param name="CodeDisplayName">The code the exception maps to, for documentation.</param>
/// <param name="Constructor">The constructor shape both helpers mirror. Never <see cref="ConstructorShape.None"/>.</param>
/// <param name="MessageIsNullable">Whether the message parameter is written as <c>string?</c> with a <c>null</c> default.</param>
/// <param name="InnerIsNullable">Whether the inner-exception parameter is written as nullable with a <c>null</c> default.</param>
/// <param name="Accessibility">
/// The helpers' accessibility keyword: <c>public</c>, unless the exception type is not itself
/// public, in which case a public factory could not return it.
/// </param>
internal sealed record ExceptionHelperModel(
    string ExceptionFqn,
    string FactoryName,
    string ThrowName,
    string CodeDisplayName,
    ConstructorShape Constructor,
    bool MessageIsNullable,
    bool InnerIsNullable,
    string Accessibility);
