using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Models;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Converts the semantic information gathered for a class decorated with one or more
/// <c>[Service]</c> attributes into an equatable <see cref="ClassRegistrationModel"/>, dropping
/// any attribute application (or the whole class) that the analyzer would report as an error, so
/// that the generator never emits code for an invalid registration.
/// </summary>
/// <remarks>
/// Only primitive data (strings, ints, bools) is carried out of this method: no
/// <see cref="ISymbol"/>, <see cref="Compilation"/>, or syntax node is retained in the returned
/// model, which is required for the incremental generator's caching to behave correctly.
/// </remarks>
internal static class ServiceAttributeParser
{
    public static ClassRegistrationModel? GetModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } classSymbol)
        {
            return null;
        }

        // SSAL001: abstract or static classes cannot be registered.
        if (classSymbol.IsAbstract || classSymbol.IsStatic)
        {
            return null;
        }

        // SSAL003: open generic classes are not supported.
        if (classSymbol.IsGenericType)
        {
            return null;
        }

        var implementationTypeFqn = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var entries = ImmutableArray.CreateBuilder<RegistrationEntryModel>(context.Attributes.Length);

        foreach (var attributeData in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = TryBuildEntry(classSymbol, implementationTypeFqn, attributeData);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        if (entries.Count == 0)
        {
            return null;
        }

        return new ClassRegistrationModel(implementationTypeFqn, entries.ToImmutable().ToEquatableArray());
    }

    private static RegistrationEntryModel? TryBuildEntry(INamedTypeSymbol classSymbol, string implementationTypeFqn, AttributeData attributeData)
    {
        var lifetime = AttributeArgumentReader.GetLifetime(attributeData);
        var mode = AttributeArgumentReader.GetMode(attributeData);

        // SSAL008: an out-of-range Lifetime/Mode must not reach the emitter, which would either
        // silently mis-render it (Lifetime) or emit nothing at all for it (Mode).
        if (lifetime is < (int)WellKnownLifetime.Singleton or > (int)WellKnownLifetime.Transient)
        {
            return null;
        }

        if (mode is < (int)WellKnownRegistrationMode.Add or > (int)WellKnownRegistrationMode.Replace)
        {
            return null;
        }

        var key = GetKey(attributeData);

        // SSAL005: no keyed TryAddEnumerable API exists.
        if (key.HasKey && mode == (int)WellKnownRegistrationMode.TryAddEnumerable)
        {
            return null;
        }

        // SSAL002: the class must implement/derive the explicitly requested (or, absent an
        // explicit "As", implicitly resolved) service type(s).
        if (!TryResolveServiceTypes(classSymbol, implementationTypeFqn, attributeData, out var serviceTypeSymbols, out var serviceTypeFqns))
        {
            return null;
        }

        // SSAL007: the implementation type and every resolved service type must be accessible
        // from the generated registration code.
        if (!TypeAccessibilityChecker.IsAccessible(classSymbol))
        {
            return null;
        }

        foreach (var serviceTypeSymbol in serviceTypeSymbols)
        {
            if (!TypeAccessibilityChecker.IsAccessible(serviceTypeSymbol))
            {
                return null;
            }
        }

        // SSAL007: a `typeof(...)` Key value must be accessible too, since it is emitted
        // verbatim into the same generated code as the implementation/service types.
        if (!IsKeyTypeAccessible(attributeData))
        {
            return null;
        }

        // SSAL006: TryAddEnumerable cannot distinguish a registration whose service type is the
        // implementation type itself.
        if (mode == (int)WellKnownRegistrationMode.TryAddEnumerable && serviceTypeFqns.Contains(implementationTypeFqn, StringComparer.Ordinal))
        {
            return null;
        }

        return new RegistrationEntryModel(serviceTypeFqns.ToEquatableArray(), lifetime, mode, key);
    }

    /// <summary>
    /// Resolves the service type(s) an attribute application registers against: the explicit
    /// <c>As</c> type (failing if the class does not implement/derive it), or otherwise every
    /// directly-implemented interface, sorted for deterministic emission order (or the
    /// implementation type itself, if it implements none).
    /// </summary>
    private static bool TryResolveServiceTypes(
        INamedTypeSymbol classSymbol,
        string implementationTypeFqn,
        AttributeData attributeData,
        out ImmutableArray<ITypeSymbol> serviceTypeSymbols,
        out ImmutableArray<string> serviceTypeFqns)
    {
        var asType = AttributeArgumentReader.GetAsType(attributeData);
        if (asType is not null)
        {
            if (!ServiceTypeResolver.Implements(classSymbol, asType))
            {
                serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
                serviceTypeFqns = ImmutableArray<string>.Empty;
                return false;
            }

            serviceTypeSymbols = ImmutableArray.Create(asType);
            serviceTypeFqns = ImmutableArray.Create(asType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            return true;
        }

        var interfaces = ServiceTypeResolver.GetDirectlyImplementedInterfaces(classSymbol);
        if (interfaces.Length == 0)
        {
            serviceTypeSymbols = ImmutableArray.Create<ITypeSymbol>(classSymbol);
            serviceTypeFqns = ImmutableArray.Create(implementationTypeFqn);
            return true;
        }

        // Sorted by FQN for deterministic emission order; both arrays must stay in lockstep, so
        // sort a single sequence of pairs rather than sorting the two projections independently.
        var ordered = interfaces
            .Select(i => (Symbol: (ITypeSymbol)i, Fqn: i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .OrderBy(pair => pair.Fqn, StringComparer.Ordinal)
            .ToImmutableArray();

        serviceTypeSymbols = ordered.Select(pair => pair.Symbol).ToImmutableArray();
        serviceTypeFqns = ordered.Select(pair => pair.Fqn).ToImmutableArray();
        return true;
    }

    private static KeyModel GetKey(AttributeData attributeData)
    {
        var constant = AttributeArgumentReader.GetKeyConstant(attributeData);
        if (constant is null)
        {
            return KeyModel.None;
        }

        var expression = KeyLiteralFormatter.Format(constant.Value);
        return expression is null ? KeyModel.None : new KeyModel(true, expression);
    }

    /// <summary>
    /// Returns <see langword="false"/> only when <c>Key</c> is a <c>typeof(...)</c> value whose
    /// type is not accessible from the generated registration code (see
    /// <see cref="TypeAccessibilityChecker"/>); any other kind of key (or no key at all) is always
    /// fine as far as accessibility is concerned.
    /// </summary>
    private static bool IsKeyTypeAccessible(AttributeData attributeData)
    {
        var constant = AttributeArgumentReader.GetKeyConstant(attributeData);
        if (constant is { IsNull: false, Kind: TypedConstantKind.Type } typedConstant
            && typedConstant.Value is ITypeSymbol keyTypeSymbol)
        {
            return TypeAccessibilityChecker.IsAccessible(keyTypeSymbol);
        }

        return true;
    }
}
