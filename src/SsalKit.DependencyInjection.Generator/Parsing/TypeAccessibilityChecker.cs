using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Determines whether a named type symbol is accessible from the generated registration code:
/// a top-level, non-derived <c>static class</c> emitted into a separate file, in the
/// <c>Microsoft.Extensions.DependencyInjection</c> namespace, in the same assembly. Shared
/// between <see cref="ServiceAttributeParser"/> and <c>ServiceAttributeAnalyzer</c> so both agree
/// on exactly which types are usable.
/// </summary>
internal static class TypeAccessibilityChecker
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> and every containing type has
    /// an effective accessibility of at least <see langword="internal"/> and is not file-local.
    /// </summary>
    /// <remarks>
    /// The generated code is not a derived class of, nor nested within, the decorated type, so
    /// <see langword="protected"/> access (on its own, i.e. not combined with
    /// <see langword="internal"/> via <c>protected internal</c>) is never sufficient: a
    /// <see langword="private"/> or <see langword="protected"/>/<c>private protected</c> nested
    /// type (or a type nested within one) cannot be referenced, and neither can a file-local type,
    /// since file-local types are only visible within their declaring file.
    /// </remarks>
    public static bool IsAccessible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            var isAtLeastInternal = current.DeclaredAccessibility
                is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal;

            if (!isAtLeastInternal)
            {
                return false;
            }
        }

        return true;
    }
}
