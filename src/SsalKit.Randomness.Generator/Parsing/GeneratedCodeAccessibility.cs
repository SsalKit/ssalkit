using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Randomness.Generator.Parsing;

/// <summary>
/// The one accessibility question the generated extension class asks that is not about a type:
/// whether it can read the decorated <i>member</i> off an instance.
/// </summary>
/// <remarks>
/// The type-level half -- "can the generated class name this type at all", "may it be declared
/// public" -- is <see cref="SymbolFacts.IsAccessibleFromGeneratedCode"/> and
/// <see cref="SymbolFacts.IsEffectivelyPublic"/> in SsalKit.Generators.Toolkit. Unlike
/// SsalKit.DependencyInjection's <c>TypeAccessibilityChecker</c>, every type inspected for
/// <c>[RandomWeight]</c> is declared in the compilation being generated for (it carries the
/// attribute in source), so cross-assembly concerns -- <c>[InternalsVisibleTo]</c> grants and
/// <c>extern alias</c> reachability -- cannot arise and are not checked.
/// </remarks>
internal static class GeneratedCodeAccessibility
{
    /// <summary>
    /// Returns <see langword="true"/> when the generated selector can read <paramref name="member"/>
    /// off an instance: the member itself, and -- for a property -- its <c>get</c> accessor, must be
    /// at least <see langword="internal"/>.
    /// </summary>
    /// <remarks>
    /// The member's own accessibility is all that matters, not the declaring type's: the selector
    /// runs inside the generated method body in the same assembly, so an <see langword="internal"/>
    /// weight member on a <see langword="public"/> type is perfectly usable and still yields
    /// <see langword="public"/> extensions.
    /// </remarks>
    public static bool IsMemberReadable(ISymbol member)
    {
        if (!SymbolFacts.IsAtLeastInternal(member.DeclaredAccessibility))
        {
            return false;
        }

        return member is not IPropertySymbol property
            || property.GetMethod is null
            || SymbolFacts.IsAtLeastInternal(property.GetMethod.DeclaredAccessibility);
    }
}
