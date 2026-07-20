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
        var key = GetKey(attributeData);

        // SSAL005: no keyed TryAddEnumerable API exists.
        if (key.HasKey && mode == (int)WellKnownRegistrationMode.TryAddEnumerable)
        {
            return null;
        }

        // SSAL002: the class must implement/derive the explicitly requested (or, absent an
        // explicit "As", implicitly resolved) service type(s).
        if (!TryResolveServiceTypeFqns(classSymbol, implementationTypeFqn, attributeData, out var serviceTypeFqns))
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
    private static bool TryResolveServiceTypeFqns(
        INamedTypeSymbol classSymbol,
        string implementationTypeFqn,
        AttributeData attributeData,
        out ImmutableArray<string> serviceTypeFqns)
    {
        var asType = AttributeArgumentReader.GetAsType(attributeData);
        if (asType is not null)
        {
            if (!ServiceTypeResolver.Implements(classSymbol, asType))
            {
                serviceTypeFqns = ImmutableArray<string>.Empty;
                return false;
            }

            serviceTypeFqns = ImmutableArray.Create(asType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            return true;
        }

        var interfaces = ServiceTypeResolver.GetDirectlyImplementedInterfaces(classSymbol);
        serviceTypeFqns = interfaces.Length == 0
            ? ImmutableArray.Create(implementationTypeFqn)
            : interfaces
                .Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToImmutableArray();
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
}
