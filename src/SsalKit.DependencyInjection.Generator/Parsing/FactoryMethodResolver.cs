using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Resolves a <c>[Service(Factory = "...")]</c> argument to the single, usable static factory
/// method it names, or classifies why none could be chosen. Shared between
/// <see cref="ServiceAttributeParser"/> and <c>ServiceAttributeAnalyzer</c> so both agree on
/// exactly which method (if any) is chosen, and on the precedence of the four ways resolution can
/// fail (SSAL011-SSAL014).
/// </summary>
internal static class FactoryMethodResolver
{
    /// <summary>
    /// Resolves <paramref name="factoryName"/> against <paramref name="classSymbol"/>.
    /// </summary>
    /// <remarks>
    /// Checked in this order, matching the diagnostic numbering: open generic class first
    /// (SSAL013, since Microsoft.Extensions.DependencyInjection has no factory API for open
    /// generics at all, regardless of what <paramref name="factoryName"/> resolves to), then
    /// whether any ordinary method with that name exists (SSAL011), then whether any of those
    /// methods has a usable shape (SSAL012), then whether the chosen usable method is accessible
    /// (SSAL014).
    /// </remarks>
    public static FactoryResolutionResult Resolve(INamedTypeSymbol classSymbol, string factoryName, bool isOpenGeneric)
    {
        if (isOpenGeneric)
        {
            return new FactoryResolutionResult(FactoryResolutionKind.OpenGenericNotSupported, null, false);
        }

        // GetMembers(name) returns only members declared directly on this type -- never ones
        // inherited from a base class -- which is exactly the "declared directly on the decorated
        // class" requirement: an inherited method can live in a different syntax tree, and
        // resolving against it would tie this class's incremental generator output to changes in
        // an unrelated file the pipeline isn't tracking as an input for this class.
        IMethodSymbol? parameterlessCandidate = null;
        IMethodSymbol? serviceProviderCandidate = null;
        var foundAnyOrdinaryMethodWithName = false;

        foreach (var member in classSymbol.GetMembers(factoryName))
        {
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
            {
                continue;
            }

            foundAnyOrdinaryMethodWithName = true;

            if (!IsUsableShape(method, classSymbol, out var acceptsServiceProvider))
            {
                continue;
            }

            if (acceptsServiceProvider)
            {
                serviceProviderCandidate = method;
            }
            else
            {
                parameterlessCandidate = method;
            }
        }

        if (!foundAnyOrdinaryMethodWithName)
        {
            // SSAL011: also reached for an empty-string Factory, since no method is ever named "".
            return new FactoryResolutionResult(FactoryResolutionKind.NotFound, null, false);
        }

        // Prefer the IServiceProvider-accepting overload when both a usable parameterless and a
        // usable IServiceProvider-accepting method exist -- deterministic, not an ambiguity error.
        // C# never allows two methods with the exact same signature in one class, so there can be
        // at most one usable candidate of each shape here.
        var chosen = serviceProviderCandidate ?? parameterlessCandidate;
        if (chosen is null)
        {
            // SSAL012: at least one method named `factoryName` exists, but none is usable.
            return new FactoryResolutionResult(FactoryResolutionKind.Invalid, null, false);
        }

        // SSAL014: the containing type's accessibility is already covered separately by SSAL007;
        // this checks only the method's own declared accessibility.
        if (chosen.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            return new FactoryResolutionResult(FactoryResolutionKind.Inaccessible, chosen, false);
        }

        return new FactoryResolutionResult(FactoryResolutionKind.Success, chosen, serviceProviderCandidate is not null);
    }

    /// <summary>
    /// Determines whether <paramref name="method"/> is a usable factory method: <see langword="static"/>,
    /// non-generic, returning exactly <paramref name="classSymbol"/> (by <see cref="SymbolEqualityComparer"/>
    /// identity, not merely something assignable to it), and either parameterless or taking a
    /// single <see cref="IServiceProvider"/> parameter with no <see langword="ref"/>/<see langword="out"/>/<c>params</c>
    /// modifier.
    /// </summary>
    private static bool IsUsableShape(IMethodSymbol method, INamedTypeSymbol classSymbol, out bool acceptsServiceProvider)
    {
        acceptsServiceProvider = false;

        if (!method.IsStatic || method.Arity != 0)
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, classSymbol))
        {
            return false;
        }

        if (method.Parameters.Length == 0)
        {
            return true;
        }

        if (method.Parameters.Length == 1)
        {
            var parameter = method.Parameters[0];
            if (parameter.RefKind == RefKind.None && !parameter.IsParams && IsServiceProviderType(parameter.Type))
            {
                acceptsServiceProvider = true;
                return true;
            }
        }

        return false;
    }

    private static bool IsServiceProviderType(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "IServiceProvider", ContainingNamespace.Name: "System", Arity: 0 } namedType
        && namedType.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;
}

/// <summary>
/// The outcome of <see cref="FactoryMethodResolver.Resolve"/>: either a chosen, usable,
/// accessible factory method (<see cref="Success"/>), or which of the four ways resolution can
/// fail applies.
/// </summary>
internal enum FactoryResolutionKind
{
    /// <summary>SSAL013: the decorated class is an open generic; 'Factory' is never supported there.</summary>
    OpenGenericNotSupported,

    /// <summary>SSAL011: no ordinary method with the given name is declared directly on the class.</summary>
    NotFound,

    /// <summary>SSAL012: one or more methods with the given name exist, but none has a usable shape.</summary>
    Invalid,

    /// <summary>SSAL014: a usable method was chosen, but it is not accessible from generated code.</summary>
    Inaccessible,

    /// <summary>A usable, accessible factory method was chosen.</summary>
    Success,
}

/// <summary>
/// The result of resolving a <c>[Service(Factory = "...")]</c> argument.
/// </summary>
/// <param name="Kind">Which outcome this is.</param>
/// <param name="Method">
/// The chosen method for <see cref="FactoryResolutionKind.Success"/> or
/// <see cref="FactoryResolutionKind.Inaccessible"/>; <see langword="null"/> otherwise.
/// </param>
/// <param name="AcceptsServiceProvider">
/// Whether the chosen method takes a single <see cref="IServiceProvider"/> parameter (as opposed
/// to none). Only meaningful when <see cref="Kind"/> is <see cref="FactoryResolutionKind.Success"/>.
/// </param>
internal readonly record struct FactoryResolutionResult(FactoryResolutionKind Kind, IMethodSymbol? Method, bool AcceptsServiceProvider);
