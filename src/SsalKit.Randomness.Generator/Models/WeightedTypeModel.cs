namespace SsalKit.Randomness.Generator.Models;

/// <summary>
/// Everything the emitter needs to write one type's extension class, reduced to primitives so the
/// incremental pipeline can compare two runs' models by value.
/// </summary>
/// <param name="Namespace">
/// The declaring type's namespace, or the empty string when it lives in the global namespace (in
/// which case no <c>namespace</c> block is emitted).
/// </param>
/// <param name="TypeFqn">The declaring type's <c>global::</c>-prefixed fully qualified name.</param>
/// <param name="ExtensionClassName">
/// The generated class name: the declaring type's name, with the names of any containing types
/// flattened in front of it (<c>Outer_Inner</c>), plus the <c>RandomWeightExtensions</c> suffix.
/// </param>
/// <param name="MemberAccess">
/// The weight member's name as it must be written in the generated selector, already
/// <c>@</c>-escaped if it collides with a keyword.
/// </param>
/// <param name="Weight">Which runtime selector overload the generated methods delegate to.</param>
/// <param name="IsPublic">
/// Whether the extension class is declared <see langword="public"/>. False when the declaring type's
/// effective accessibility is below public, or when <c>[RandomWeight(InternalExtensions = true)]</c>
/// forced it down.
/// </param>
/// <param name="HintName">The <c>AddSource</c> hint name for this type's generated file.</param>
internal sealed record WeightedTypeModel(
    string Namespace,
    string TypeFqn,
    string ExtensionClassName,
    string MemberAccess,
    WeightKind Weight,
    bool IsPublic,
    string HintName);

/// <summary>
/// Which family of runtime weighted-picking overloads a weight member's type can delegate to, and
/// therefore which methods get generated.
/// </summary>
internal enum WeightKind
{
    /// <summary>
    /// <see langword="sbyte"/>, <see langword="byte"/>, <see langword="short"/>,
    /// <see langword="ushort"/>, <see langword="int"/>, <see langword="uint"/>, or
    /// <see langword="long"/>: delegates to the <c>Func&lt;T, long&gt;</c> overloads, which cover
    /// single draws, batched draws, and alias-table sampling.
    /// </summary>
    Integral,

    /// <summary>
    /// <see langword="float"/> or <see langword="double"/>: delegates to the
    /// <c>Func&lt;T, double&gt;</c> overload, of which the runtime only offers a single-draw form.
    /// </summary>
    Floating,
}
