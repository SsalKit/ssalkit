using Microsoft.CodeAnalysis;

namespace SsalKit.Randomness.Generator.Parsing;

/// <summary>
/// Decides what the generated extension class -- a top-level static class emitted into a separate
/// file, in the declaring type's namespace, in the same assembly -- can see, and how visible it may
/// itself be declared.
/// </summary>
/// <remarks>
/// Unlike SsalKit.DependencyInjection's <c>TypeAccessibilityChecker</c>, every type inspected here
/// is declared in the compilation being generated for (it carries a <c>[RandomWeight]</c> member in
/// source), so cross-assembly concerns -- <c>[InternalsVisibleTo]</c> grants and
/// <c>extern alias</c> reachability -- cannot arise and are not checked.
/// </remarks>
internal static class GeneratedCodeAccessibility
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> and every type containing it are
    /// at least <see langword="internal"/> and not file-local, i.e. nameable from the generated
    /// class.
    /// </summary>
    public static bool IsTypeVisible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal || !IsAtLeastInternal(current.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }

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
        if (!IsAtLeastInternal(member.DeclaredAccessibility))
        {
            return false;
        }

        return member is not IPropertySymbol property
            || property.GetMethod is null
            || IsAtLeastInternal(property.GetMethod.DeclaredAccessibility);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> and every type containing it are
    /// <see langword="public"/>, which is the only case where the generated class may itself be
    /// declared <see langword="public"/> without an inconsistent-accessibility error.
    /// </summary>
    public static bool IsEffectivelyPublic(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    /// <remarks>
    /// <c>protected internal</c> counts (the generated class benefits from the <c>internal</c>
    /// half), but <c>private protected</c> does not: the generated class derives from nothing.
    /// </remarks>
    private static bool IsAtLeastInternal(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
}
