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
    /// generated registration code in <paramref name="compilation"/>.
    /// </summary>
    /// <remarks>
    /// The generated code is not a derived class of, nor nested within, the decorated type, so
    /// <see langword="protected"/> access (on its own, i.e. not combined with
    /// <see langword="internal"/> via <c>protected internal</c>) is never sufficient: a
    /// <see langword="private"/> or <see langword="protected"/>/<c>private protected</c> nested
    /// type (or a type nested within one) cannot be referenced, and neither can a file-local type,
    /// since file-local types are only visible within their declaring file.
    /// </remarks>
    public static bool IsAccessible(ITypeSymbol type, Compilation compilation)
    {
        return type switch
        {
            INamedTypeSymbol namedType => IsNamedTypeAccessible(namedType, compilation),
            IArrayTypeSymbol arrayType => IsAccessible(arrayType.ElementType, compilation),
            IPointerTypeSymbol pointerType => IsAccessible(pointerType.PointedAtType, compilation),
            IFunctionPointerTypeSymbol functionPointerType => IsFunctionPointerAccessible(functionPointerType, compilation),
            // A type parameter has no accessibility of its own and is always fine to reference.
            // In practice this is unreachable for a resolved service/key type here (the decorated
            // class is never itself an open generic -- see SSAL003 -- so every type argument it can
            // contribute is closed), but is handled defensively for robustness.
            ITypeParameterSymbol => true,
            // `dynamic` and any other exotic symbol kind that could in principle reach here via a
            // `typeof(...)` Key: none of these have a meaningful "containing type" accessibility
            // chain, so there is nothing to reject.
            _ => true,
        };
    }

    /// <summary>
    /// Recurses into a function pointer type's return type and parameter types. Unlike an ordinary
    /// pointer, a <c>typeof(...)</c> Key value can never actually be a function pointer type in
    /// practice: <c>typeof(delegate*&lt;...&gt;)</c> is rejected by the C# compiler itself with
    /// CS8911 ("cannot take a delegate pointer as an argument to typeof"), so this path is not
    /// reachable via any <c>[Service]</c> attribute argument today. Handled anyway, defensively, in
    /// case that restriction is ever lifted or another symbol-producing path is added.
    /// </summary>
    private static bool IsFunctionPointerAccessible(IFunctionPointerTypeSymbol type, Compilation compilation)
    {
        var signature = type.Signature;

        if (!IsAccessible(signature.ReturnType, compilation))
        {
            return false;
        }

        foreach (var parameter in signature.Parameters)
        {
            if (!IsAccessible(parameter.Type, compilation))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks <paramref name="type"/> and every containing type for an effective accessibility of
    /// at least <see langword="internal"/> and non-file-local, and recursively checks every
    /// generic type argument (of <paramref name="type"/> and of each containing type) the same
    /// way, so a type like <c>IHandler&lt;PrivateNested&gt;</c> or
    /// <c>typeof(List&lt;PrivateNested&gt;)</c> is correctly rejected even though
    /// <c>IHandler</c>/<c>List&lt;&gt;</c> itself is public.
    /// </summary>
    private static bool IsNamedTypeAccessible(INamedTypeSymbol type, Compilation compilation)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            if (!IsAssemblyReachable(current.ContainingAssembly, compilation))
            {
                return false;
            }

            var isAccessible = current.DeclaredAccessibility switch
            {
                Accessibility.Public => true,
                // `internal`/`protected internal` are only actually usable from the generated
                // code -- a top-level type in the *current* compilation's assembly -- when that
                // assembly either *is* the type's own assembly, or has been granted access via
                // [InternalsVisibleTo]. A `protected internal` nested type in another assembly's
                // base class can be perfectly legal to name at the [Service] attribute application
                // site (a class deriving from that base class gets the "protected" half of the
                // grant), but the generated top-level static class is never such a derived class,
                // so only the "internal" half can ever apply to it.
                Accessibility.Internal or Accessibility.ProtectedOrInternal =>
                    IsSameOrGivenAccessTo(current.ContainingAssembly, compilation),
                _ => false,
            };

            if (!isAccessible)
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
                if (!IsAccessible(typeArgument, compilation))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="containingAssembly"/> is the current
    /// compilation's own assembly, or has granted it access via <c>[InternalsVisibleTo]</c>.
    /// </summary>
    private static bool IsSameOrGivenAccessTo(IAssemblySymbol? containingAssembly, Compilation compilation)
    {
        if (containingAssembly is null || SymbolEqualityComparer.Default.Equals(containingAssembly, compilation.Assembly))
        {
            return true;
        }

        return containingAssembly.GivesAccessTo(compilation.Assembly);
    }

    /// <summary>
    /// Returns <see langword="false"/> when <paramref name="containingAssembly"/> is only
    /// reachable through an <c>extern alias</c> (i.e. every <see cref="MetadataReference"/> that
    /// contributes it to the compilation uses a non-<c>global</c> alias). The generated code emits
    /// only <c>global::</c>-qualified names and never an <c>extern alias</c> directive, so a type
    /// from such an assembly cannot be named there at all, regardless of its declared
    /// accessibility: referencing it would either fail to compile (CS0400) or silently bind to an
    /// unrelated type of the same fully-qualified name visible through the global alias.
    /// </summary>
    private static bool IsAssemblyReachable(IAssemblySymbol? containingAssembly, Compilation compilation)
    {
        if (containingAssembly is null || SymbolEqualityComparer.Default.Equals(containingAssembly, compilation.Assembly))
        {
            return true;
        }

        var reference = compilation.GetMetadataReference(containingAssembly);
        if (reference is null)
        {
            // No corresponding MetadataReference could be resolved for this assembly symbol (can
            // happen for some merged/forwarded corlib scenarios); do not reject on this basis
            // alone, to avoid a false positive against every ordinary BCL type.
            return true;
        }

        var aliases = reference.Properties.Aliases;
        return aliases.IsEmpty || aliases.Contains(MetadataReferenceProperties.GlobalAlias);
    }
}
