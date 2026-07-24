using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Renders the "typeof-form" spelling of an open generic type -- e.g.
/// <c>global::Ns.IRepository&lt;&gt;</c> for arity 1, <c>global::Ns.IThing&lt;,&gt;</c> for arity
/// 2 -- used wherever an open generic <c>[Service]</c> registration must pass a service or
/// implementation type as a runtime <see cref="System.Type"/> (<c>typeof(...)</c>) rather than as
/// a closed generic type argument.
/// </summary>
/// <remarks>
/// Works uniformly for the class's own (non-unbound) generic definition, an implemented interface
/// or base class instantiated with the class's own type parameters as arguments, and an explicit
/// unbound <c>typeof(X&lt;&gt;)</c> <c>As</c> value alike: all three carry the same
/// <see cref="INamedTypeSymbol.Name"/>/<see cref="INamedTypeSymbol.ContainingNamespace"/>/
/// <see cref="INamedTypeSymbol.ContainingType"/>/<see cref="INamedTypeSymbol.Arity"/>, which is all
/// this formatter looks at -- their type arguments (which do differ) are never rendered.
/// </remarks>
internal static class OpenGenericTypeofFormatter
{
    // Renders namespaces, containing types, and keyword-escaping exactly like
    // SymbolDisplayFormat.FullyQualifiedFormat, but omits the type parameter/argument list
    // entirely so the arity placeholder ("<>", "<,>", ...) can be appended manually below.
    private static readonly SymbolDisplayFormat DefinitionFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None);

    /// <summary>
    /// Formats <paramref name="type"/> in typeof-form.
    /// </summary>
    public static string Format(INamedTypeSymbol type)
    {
        var name = type.ToDisplayString(DefinitionFormat);
        return type.Arity == 0 ? name : name + FormatArityPlaceholder(type.Arity);
    }

    private static string FormatArityPlaceholder(int arity) => $"<{new string(',', arity - 1)}>";
}
