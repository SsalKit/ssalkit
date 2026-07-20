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
    /// <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>. An empty result means the
    /// class should be registered as itself.
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetDirectlyImplementedInterfaces(INamedTypeSymbol classSymbol)
    {
        if (classSymbol.Interfaces.IsDefaultOrEmpty)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(classSymbol.Interfaces.Length);
        foreach (var iface in classSymbol.Interfaces)
        {
            if (IsDisposableOrAsyncDisposable(iface))
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
}
