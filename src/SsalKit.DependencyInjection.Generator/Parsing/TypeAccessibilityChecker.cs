using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Determines whether a type symbol -- an implementation type, a resolved service type, or a
/// <c>typeof(...)</c> <c>Key</c> value, including any generic type arguments it carries -- is
/// accessible from the generated registration code: a top-level, non-derived <c>static class</c>
/// emitted into a separate file, in the <c>Microsoft.Extensions.DependencyInjection</c> namespace,
/// in the same assembly. Shared between <see cref="ServiceAttributeParser"/> and
/// <c>ServiceAttributeAnalyzer</c> so both agree on exactly which types are usable.
/// </summary>
internal static class TypeAccessibilityChecker
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is accessible from the
    /// generated registration code.
    /// </summary>
    /// <remarks>
    /// The generated code is not a derived class of, nor nested within, the decorated type, so
    /// <see langword="protected"/> access (on its own, i.e. not combined with
    /// <see langword="internal"/> via <c>protected internal</c>) is never sufficient: a
    /// <see langword="private"/> or <see langword="protected"/>/<c>private protected</c> nested
    /// type (or a type nested within one) cannot be referenced, and neither can a file-local type,
    /// since file-local types are only visible within their declaring file.
    /// </remarks>
    public static bool IsAccessible(ITypeSymbol type)
    {
        return type switch
        {
            INamedTypeSymbol namedType => IsNamedTypeAccessible(namedType),
            IArrayTypeSymbol arrayType => IsAccessible(arrayType.ElementType),
            // A type parameter has no accessibility of its own and is always fine to reference.
            // In practice this is unreachable for a resolved service/key type here (the decorated
            // class is never itself an open generic -- see SSAL003 -- so every type argument it can
            // contribute is closed), but is handled defensively for robustness.
            ITypeParameterSymbol => true,
            // Pointers, function pointers, `dynamic`, and any other exotic symbol kind that could
            // in principle reach here via a `typeof(...)` Key: none of these have a meaningful
            // "containing type" accessibility chain, so there is nothing to reject.
            _ => true,
        };
    }

    /// <summary>
    /// Checks <paramref name="type"/> and every containing type for an effective accessibility of
    /// at least <see langword="internal"/> and non-file-local, and recursively checks every
    /// generic type argument (of <paramref name="type"/> and of each containing type) the same
    /// way, so a type like <c>IHandler&lt;PrivateNested&gt;</c> or
    /// <c>typeof(List&lt;PrivateNested&gt;)</c> is correctly rejected even though
    /// <c>IHandler</c>/<c>List&lt;&gt;</c> itself is public.
    /// </summary>
    private static bool IsNamedTypeAccessible(INamedTypeSymbol type)
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

            // An unbound generic type definition (e.g. `typeof(List<>)`) reports its own type
            // *parameters* back as "type arguments", but represented as placeholder ErrorType
            // symbols rather than an ITypeParameterSymbol -- there is nothing meaningful to check
            // there, and it must not be rejected as if it were an inaccessible type argument.
            if (current.IsUnboundGenericType)
            {
                continue;
            }

            foreach (var typeArgument in current.TypeArguments)
            {
                if (!IsAccessible(typeArgument))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
