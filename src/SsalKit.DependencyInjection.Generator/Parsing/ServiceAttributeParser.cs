using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.Generators.Toolkit;

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

        // SSAL003: a class nested inside a generic type carries its containing type's type
        // parameters and can never be registered as an open generic, regardless of its own arity.
        if (ServiceTypeResolver.IsNestedInGenericType(classSymbol))
        {
            return null;
        }

        var isOpenGeneric = classSymbol.Arity > 0;

        // Typeof-form (e.g. "global::Ns.Repository<>") for an open generic class -- this is what
        // gets spliced into `typeof(...)` in the generated code, since a plain FullyQualifiedFormat
        // display would render the class's own type parameter names (e.g. "Repository<T>"), which
        // do not exist as symbols in the generated extension method's scope.
        var implementationTypeFqn = isOpenGeneric
            ? OpenGenericTypeofFormatter.Format(classSymbol)
            : classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Only used transiently within this method (and methods it calls) to evaluate
        // accessibility -- never retained in the returned model, per the incremental-caching
        // requirement described in the remarks above.
        var compilation = context.SemanticModel.Compilation;

        var entries = ImmutableArray.CreateBuilder<RegistrationEntryModel>(context.Attributes.Length);

        foreach (var attributeData in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = TryBuildEntry(classSymbol, implementationTypeFqn, isOpenGeneric, attributeData, compilation);
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

    private static RegistrationEntryModel? TryBuildEntry(
        INamedTypeSymbol classSymbol, string implementationTypeFqn, bool isOpenGeneric, AttributeData attributeData, Compilation compilation)
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

        // SSAL011/SSAL012/SSAL013/SSAL014: resolve (or drop the entry for) an explicit 'Factory'.
        // Independent of Key/Mode/As, so it is checked here, before service-type resolution, in
        // lockstep with ServiceAttributeAnalyzer.
        if (!TryResolveFactory(classSymbol, isOpenGeneric, attributeData, out var factory))
        {
            return null;
        }

        // SSAL002/SSAL009: the class must implement/derive the explicitly requested (or, absent an
        // explicit "As", implicitly resolved) service type(s); for an open generic class, each
        // resolved service type must additionally be an exact-match shape (SSAL009).
        if (!TryResolveServiceTypes(classSymbol, implementationTypeFqn, isOpenGeneric, attributeData, out var serviceTypeSymbols, out var serviceTypeFqns))
        {
            return null;
        }

        // SSAL007: the implementation type and every resolved service type must be accessible
        // from the generated registration code.
        if (!TypeAccessibilityChecker.IsAccessible(classSymbol, compilation))
        {
            return null;
        }

        foreach (var serviceTypeSymbol in serviceTypeSymbols)
        {
            if (!TypeAccessibilityChecker.IsAccessible(serviceTypeSymbol, compilation))
            {
                return null;
            }
        }

        // SSAL007: a `typeof(...)` Key value must be accessible too, since it is emitted
        // verbatim into the same generated code as the implementation/service types.
        if (!IsKeyTypeAccessible(attributeData, compilation))
        {
            return null;
        }

        // SSAL006: TryAddEnumerable cannot distinguish a registration whose service type is the
        // implementation type itself. Symbol-based (via ServiceTypeResolver.IsSelfServiceType),
        // not an FQN string comparison, to stay in lockstep with the analyzer's mirrored check:
        // the typeof-form FQN strings computed here already happen to string-match for every case
        // reachable today (both implementationTypeFqn and an unbound `As = typeof(C<>)`'s FQN are
        // rendered by the same OpenGenericTypeofFormatter from the same underlying definition), but
        // relying on that coincidence would leave the parser one FQN-rendering change away from
        // silently diverging from the analyzer again.
        if (mode == (int)WellKnownRegistrationMode.TryAddEnumerable
            && serviceTypeSymbols.Any(serviceTypeSymbol => ServiceTypeResolver.IsSelfServiceType(classSymbol, serviceTypeSymbol)))
        {
            return null;
        }

        return new RegistrationEntryModel(serviceTypeFqns.ToEquatableArray(), lifetime, mode, key, isOpenGeneric, factory);
    }

    /// <summary>
    /// Resolves the <c>Factory</c> named argument, if any, mirroring
    /// <c>ServiceAttributeAnalyzer</c>'s validation exactly (via the shared
    /// <see cref="FactoryMethodResolver"/>) but dropping the entry silently instead of reporting a
    /// diagnostic: no <c>Factory</c> argument is always valid (<paramref name="factory"/> is
    /// <see cref="FactoryModel.None"/>); an open generic class with a <c>Factory</c> (SSAL013), a
    /// name that matches no ordinary method (SSAL011), a name whose methods are all unusable
    /// (SSAL012), or a usable method that isn't accessible from generated code (SSAL014) all fail
    /// the whole attribute application.
    /// </summary>
    private static bool TryResolveFactory(
        INamedTypeSymbol classSymbol, bool isOpenGeneric, AttributeData attributeData, out FactoryModel factory)
    {
        var factoryName = AttributeArgumentReader.GetFactoryName(attributeData);
        if (factoryName is null)
        {
            factory = FactoryModel.None;
            return true;
        }

        var resolution = FactoryMethodResolver.Resolve(classSymbol, factoryName, isOpenGeneric);
        if (resolution.Kind != FactoryResolutionKind.Success)
        {
            factory = FactoryModel.None;
            return false;
        }

        factory = new FactoryModel(true, factoryName, resolution.AcceptsServiceProvider);
        return true;
    }

    /// <summary>
    /// Resolves the service type(s) an attribute application registers against: the explicit
    /// <c>As</c> type (failing if the class does not implement/derive it), or otherwise every
    /// directly-implemented interface, sorted for deterministic emission order (or the
    /// implementation type itself, if it implements none). For an open generic class, every
    /// resolved service type must additionally be an exact-match shape (SSAL009); this mirrors
    /// <c>ServiceAttributeAnalyzer</c>'s validation exactly, dropping the entry silently instead of
    /// reporting a diagnostic.
    /// </summary>
    private static bool TryResolveServiceTypes(
        INamedTypeSymbol classSymbol,
        string implementationTypeFqn,
        bool isOpenGeneric,
        AttributeData attributeData,
        out ImmutableArray<ITypeSymbol> serviceTypeSymbols,
        out ImmutableArray<string> serviceTypeFqns)
    {
        var asType = AttributeArgumentReader.GetAsType(attributeData);
        if (asType is not null)
        {
            if (isOpenGeneric)
            {
                return TryResolveOpenGenericAsType(classSymbol, asType, out serviceTypeSymbols, out serviceTypeFqns);
            }

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

        if (isOpenGeneric)
        {
            // SSAL009: every directly-implemented interface must be an exact-match open generic
            // service type; the whole attribute application is dropped (no partial skipping) if
            // any one of them isn't -- the escape hatch is an explicit `As`.
            foreach (var iface in interfaces)
            {
                if (!ServiceTypeResolver.IsExactMatchOpenGenericServiceType(classSymbol, iface))
                {
                    serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
                    serviceTypeFqns = ImmutableArray<string>.Empty;
                    return false;
                }
            }

            // Typeof-form for every candidate (each interface's own generic definition, not the
            // class's substituted type arguments), sorted by that same typeof-form for
            // deterministic emission order.
            var orderedOpen = interfaces
                .Select(i => (Symbol: (ITypeSymbol)i, Fqn: OpenGenericTypeofFormatter.Format(i)))
                .OrderBy(pair => pair.Fqn, StringComparer.Ordinal)
                .ToImmutableArray();

            serviceTypeSymbols = orderedOpen.Select(pair => pair.Symbol).ToImmutableArray();
            serviceTypeFqns = orderedOpen.Select(pair => pair.Fqn).ToImmutableArray();
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

    /// <summary>
    /// Resolves an explicit <c>As = typeof(X&lt;&gt;)</c> service type applied to an open generic
    /// class, mirroring <c>ServiceAttributeAnalyzer.TryResolveOpenGenericAsType</c>'s validation
    /// (a closed/non-generic <c>As</c> value, an <c>As</c> the class does not implement/derive any
    /// instantiation of, or an implemented instantiation that isn't an exact-match shape, are all
    /// dropped silently here instead of reported).
    /// </summary>
    private static bool TryResolveOpenGenericAsType(
        INamedTypeSymbol classSymbol,
        ITypeSymbol asType,
        out ImmutableArray<ITypeSymbol> serviceTypeSymbols,
        out ImmutableArray<string> serviceTypeFqns)
    {
        serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
        serviceTypeFqns = ImmutableArray<string>.Empty;

        if (asType is not INamedTypeSymbol { IsUnboundGenericType: true } unboundAsType)
        {
            return false;
        }

        var instantiation = ServiceTypeResolver.FindOpenGenericAsInstantiation(classSymbol, unboundAsType);
        if (instantiation is null || !ServiceTypeResolver.IsExactMatchOpenGenericServiceType(classSymbol, instantiation))
        {
            return false;
        }

        serviceTypeSymbols = ImmutableArray.Create<ITypeSymbol>(unboundAsType);
        serviceTypeFqns = ImmutableArray.Create(OpenGenericTypeofFormatter.Format(unboundAsType));
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
    private static bool IsKeyTypeAccessible(AttributeData attributeData, Compilation compilation)
    {
        var constant = AttributeArgumentReader.GetKeyConstant(attributeData);
        if (constant is { IsNull: false, Kind: TypedConstantKind.Type } typedConstant
            && typedConstant.Value is ITypeSymbol keyTypeSymbol)
        {
            return TypeAccessibilityChecker.IsAccessible(keyTypeSymbol, compilation);
        }

        return true;
    }
}
