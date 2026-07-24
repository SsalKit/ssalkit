using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Resolves the set of service types a class should be registered as when a <c>[Service]</c>
/// attribute does not specify an explicit <c>As</c> type. Shared between the generator (which
/// needs the resolved types to emit registration code) and the analyzer (which needs the same
/// set to detect duplicate registrations across the compilation).
/// </summary>
internal static class ServiceTypeResolver
{
    /// <summary>
    /// Returns the class's directly-implemented interfaces (i.e. those listed on the class's own
    /// base-list, not ones only implemented by a base class), excluding
    /// <see cref="IDisposable"/>, <see cref="IAsyncDisposable"/>, and (for a <c>record class</c>)
    /// the compiler-synthesized <c>IEquatable&lt;TSelf&gt;</c>. An empty result means the class
    /// should be registered as itself.
    /// </summary>
    /// <remarks>
    /// A record class implicitly implements <c>IEquatable&lt;TSelf&gt;</c> as part of its
    /// compiler-generated equality members, and that interface appears in
    /// <see cref="INamedTypeSymbol.Interfaces"/> exactly as if it had been listed by hand. Without
    /// this exclusion, every <c>[Service]</c> record would silently gain a nonsensical
    /// registration of itself as <c>IEquatable&lt;TSelf&gt;</c> and -- with 2+ real interfaces --
    /// would incorrectly tip a Singleton/Scoped registration into the forwarding path. The
    /// exclusion is gated on <see cref="INamedTypeSymbol.IsRecord"/> so it never touches an
    /// ordinary <c>class</c> that deliberately implements <c>IEquatable&lt;TSelf&gt;</c> by hand --
    /// for such a class, that interface is a real, intentional service type like any other.
    /// </remarks>
    public static ImmutableArray<INamedTypeSymbol> GetDirectlyImplementedInterfaces(INamedTypeSymbol classSymbol)
    {
        if (classSymbol.Interfaces.IsDefaultOrEmpty)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(classSymbol.Interfaces.Length);
        foreach (var iface in classSymbol.Interfaces)
        {
            if (IsDisposableOrAsyncDisposable(iface) || (classSymbol.IsRecord && IsSelfIEquatable(iface, classSymbol)))
            {
                continue;
            }

            builder.Add(iface);
        }

        return builder.ToImmutable();
    }

    private static bool IsDisposableOrAsyncDisposable(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_IDisposable)
        {
            return true;
        }

        return type is { Name: "IAsyncDisposable", ContainingNamespace.Name: "System" }
            && type.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;
    }

    private static bool IsSelfIEquatable(INamedTypeSymbol type, INamedTypeSymbol classSymbol)
    {
        return type is { Name: "IEquatable", ContainingNamespace.Name: "System", TypeArguments.Length: 1 }
            && type.ContainingNamespace.ContainingNamespace.IsGlobalNamespace
            && SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], classSymbol);
    }

    /// <summary>
    /// Determines whether <paramref name="classSymbol"/> implements or derives from
    /// <paramref name="asType"/>, i.e. whether an <c>As = typeof(asType)</c> declaration is valid
    /// for this class.
    /// </summary>
    public static bool Implements(INamedTypeSymbol classSymbol, ITypeSymbol asType)
    {
        if (SymbolEqualityComparer.Default.Equals(classSymbol, asType))
        {
            return true;
        }

        if (asType.TypeKind == TypeKind.Interface)
        {
            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, asType))
                {
                    return true;
                }
            }

            return false;
        }

        for (var baseType = classSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, asType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> is nested (at any depth) inside a generic type,
    /// i.e. whether any of its containing types has type parameters of its own.
    /// </summary>
    /// <remarks>
    /// <see cref="INamedTypeSymbol.IsGenericType"/> is true both for a type with its own type
    /// parameters and for a non-generic type nested inside a generic one, so it cannot by itself
    /// distinguish the two. This method looks only at the *containing* types' own arity, which is
    /// exactly the distinction SSAL003 needs: a class whose own arity is greater than zero but
    /// whose containing types (if any) are all non-generic carries only its own type parameters
    /// and can be supported as an open generic service; a class nested inside a generic type -- at
    /// any depth, regardless of its own arity -- additionally carries its container's type
    /// parameters and can never be registered as one.
    /// </remarks>
    public static bool IsNestedInGenericType(INamedTypeSymbol type)
    {
        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (containing.Arity > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether <paramref name="candidate"/> -- the class itself, a directly-implemented
    /// interface, or a base class -- is a valid open generic service type for the open generic
    /// class <paramref name="classSymbol"/>: either <paramref name="classSymbol"/> itself, or a
    /// type that is not itself nested inside a generic type and whose type arguments are exactly
    /// <paramref name="classSymbol"/>'s own type parameters, in declaration order (SSAL009).
    /// </summary>
    /// <remarks>
    /// Microsoft.Extensions.DependencyInjection resolves an open generic registration by
    /// substituting the requested closed service type's arguments positionally into the open
    /// generic implementation type; this is only correct when the service type's arguments are
    /// exactly the implementation's own type parameters, in the same order -- a closed, reordered,
    /// partially-applied, wrapped, or arity-mismatched service type would either fail to construct
    /// or produce a type that does not actually implement/derive the requested service.
    /// </remarks>
    public static bool IsExactMatchOpenGenericServiceType(INamedTypeSymbol classSymbol, INamedTypeSymbol candidate)
    {
        if (SymbolEqualityComparer.Default.Equals(candidate, classSymbol))
        {
            return true;
        }

        if (IsNestedInGenericType(candidate))
        {
            return false;
        }

        var typeParameters = classSymbol.TypeParameters;
        if (candidate.Arity != typeParameters.Length)
        {
            return false;
        }

        var typeArguments = candidate.TypeArguments;
        for (var i = 0; i < typeArguments.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(typeArguments[i], typeParameters[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// For <c>As = typeof(X&lt;&gt;)</c> (an unbound generic type reference) applied to an open
    /// generic class, finds the class's own instantiation of <c>X</c> -- the specific implemented
    /// interface or base class whose generic definition matches <paramref name="unboundAsType"/>
    /// -- or <see langword="null"/> if the class does not implement/derive any instantiation of it
    /// at all (SSAL002). <c>As = typeof(C&lt;&gt;)</c>, referring to the class's own definition,
    /// resolves to <paramref name="classSymbol"/> itself (self registration).
    /// </summary>
    /// <remarks>
    /// An unbound generic type symbol's <see cref="ITypeSymbol.OriginalDefinition"/> is the true
    /// generic type definition it refers to (the same definition any of the class's own
    /// implemented/derived instantiations of it also reduce to via their own
    /// <see cref="ITypeSymbol.OriginalDefinition"/>), even though the unbound symbol itself is
    /// never <see cref="SymbolEqualityComparer"/>-equal to a bound instantiation or to the
    /// definition symbol directly.
    /// </remarks>
    public static INamedTypeSymbol? FindOpenGenericAsInstantiation(INamedTypeSymbol classSymbol, INamedTypeSymbol unboundAsType)
    {
        var unboundDefinition = unboundAsType.OriginalDefinition;

        if (SymbolEqualityComparer.Default.Equals(unboundDefinition, classSymbol))
        {
            return classSymbol;
        }

        if (unboundAsType.TypeKind == TypeKind.Interface)
        {
            // A class can implement 2+ distinct instantiations of the same generic interface
            // definition (e.g. `class C<T> : IRepo<string>, IRepo<T>`). AllInterfaces'
            // enumeration order is not something callers control or should have to care about,
            // so it must not decide the outcome: if ANY implemented instantiation is an
            // exact-match shape, that one wins regardless of where it appears in the list. Only
            // when none of them conform is the first one found returned, so SSAL009 has a
            // concrete (if arbitrary) offending instantiation to name.
            INamedTypeSymbol? firstMatch = null;

            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, unboundDefinition))
                {
                    continue;
                }

                firstMatch ??= iface;

                if (IsExactMatchOpenGenericServiceType(classSymbol, iface))
                {
                    return iface;
                }
            }

            return firstMatch;
        }

        // A class has at most one base type at each level, so no analogous ordering ambiguity
        // exists here: there is only ever one instantiation of a given base type definition to
        // find.
        for (var baseType = classSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, unboundDefinition))
            {
                return baseType;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether <paramref name="serviceTypeSymbol"/> denotes <paramref name="classSymbol"/>
    /// itself -- either literally (self registration with no interfaces, or an explicit <c>As</c>
    /// pointing directly at the class), or via an unbound generic reference to the class's own
    /// definition (e.g. <c>As = typeof(C&lt;&gt;)</c> on open generic class <c>C&lt;T&gt;</c>,
    /// resolved by <see cref="FindOpenGenericAsInstantiation"/> to <paramref name="classSymbol"/>
    /// but still passed through to the emitter/analyzer as the original unbound symbol so its
    /// typeof-form renders correctly). Both spellings must be recognized as "self" for SSAL006
    /// (TryAddEnumerable cannot register a type as its own service type) to fire correctly.
    /// </summary>
    public static bool IsSelfServiceType(INamedTypeSymbol classSymbol, ITypeSymbol serviceTypeSymbol)
    {
        if (SymbolEqualityComparer.Default.Equals(serviceTypeSymbol, classSymbol))
        {
            return true;
        }

        return serviceTypeSymbol is INamedTypeSymbol namedServiceType
            && SymbolEqualityComparer.Default.Equals(namedServiceType.OriginalDefinition, classSymbol);
    }
}
