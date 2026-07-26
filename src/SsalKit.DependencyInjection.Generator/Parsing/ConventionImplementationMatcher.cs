using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// One service type a class was matched under by an <c>[assembly: RegisterImplementationsOf]</c>
/// contract.
/// </summary>
/// <param name="ServiceTypeFqn">
/// The matched interface's fully-qualified spelling, in typeof-form when
/// <see cref="IsOpenGeneric"/>.
/// </param>
/// <param name="IsOpenGeneric">
/// Whether this match pairs an open generic service type with an open generic implementation type,
/// and must therefore be emitted through the <c>Type</c>-based registration overloads.
/// </param>
internal readonly record struct ConventionServiceTypeMatch(string ServiceTypeFqn, bool IsOpenGeneric);

/// <summary>
/// Decides which classes an <c>[assembly: RegisterImplementationsOf]</c> contract matches, and
/// under which service type(s). Shared between <c>RegisterImplementationsOfAnalyzer</c> (which
/// needs the match set to know whether a contract matched anything at all, and whether two
/// contracts overlap) and <c>ConventionScanner</c> (which needs it to emit the registrations), so
/// the two can never disagree about what a scan found.
/// </summary>
internal static class ConventionImplementationMatcher
{
    /// <summary>
    /// Determines whether <paramref name="type"/> is eligible to be matched by any convention scan
    /// at all -- i.e. whether it is a concrete class the generated code could actually register.
    /// </summary>
    /// <remarks>
    /// Every rejection here is silent by design: a convention scan describes a shape rather than a
    /// specific type, so a class that simply does not fit is passed over. The conditions mirror the
    /// ones that make an explicit <c>[Service]</c> an error -- SSAL001 (abstract/static), SSAL003
    /// (nested inside a generic type), SSAL007 (not accessible from the generated code) -- plus the
    /// "explicit beats convention" rule: a class carrying at least one <c>[Service]</c> is excluded
    /// from every scan, so its explicit registration is never duplicated or contradicted, which
    /// also makes <c>[Service]</c> the per-class opt-out.
    /// </remarks>
    public static bool IsCandidate(INamedTypeSymbol type, INamedTypeSymbol? serviceAttributeSymbol, Compilation compilation)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic)
        {
            return false;
        }

        if (ServiceTypeResolver.IsNestedInGenericType(type))
        {
            return false;
        }

        if (HasServiceAttribute(type, serviceAttributeSymbol))
        {
            return false;
        }

        return TypeAccessibilityChecker.IsAccessible(type, compilation);
    }

    /// <summary>
    /// Returns every service type <paramref name="candidate"/> is registered under for
    /// <paramref name="declaration"/>, sorted by fully-qualified name so emission order never
    /// depends on <see cref="INamedTypeSymbol.AllInterfaces"/>'s enumeration order. Empty when the
    /// class does not implement the contract, or implements it only in a shape that cannot be
    /// registered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="INamedTypeSymbol.AllInterfaces"/> -- not just the directly-implemented ones a
    /// <c>[Service]</c> without <c>As</c> resolves against: the question a contract asks is
    /// "does this class implement X", and a class that gets X from its base class implements it
    /// just as much as one that lists it itself.
    /// </para>
    /// <para>
    /// A closed class matches every instantiation of an unbound contract it implements, and is
    /// registered once per instantiation. An open generic class instead reuses the exact-match rule
    /// that governs an open generic <c>[Service]</c> registration (SSAL009): only an instantiation
    /// whose type arguments are exactly the class's own type parameters, in declaration order, can
    /// be expressed as a <c>typeof</c>-based open generic registration, so anything else -- a
    /// partially-applied instantiation such as <c>Handler&lt;T&gt; : IHandler&lt;T, int&gt;</c>, or
    /// a non-generic/closed contract implemented by a generic class -- is skipped.
    /// </para>
    /// </remarks>
    public static ImmutableArray<ConventionServiceTypeMatch> Match(
        INamedTypeSymbol candidate, in ContractDeclaration declaration, Compilation compilation)
    {
        var contract = declaration.Contract;
        if (contract is null)
        {
            return ImmutableArray<ConventionServiceTypeMatch>.Empty;
        }

        var contractDefinition = contract.OriginalDefinition;
        var candidateIsOpenGeneric = candidate.Arity > 0;

        ImmutableArray<ConventionServiceTypeMatch>.Builder? builder = null;

        foreach (var iface in candidate.AllInterfaces)
        {
            var matches = declaration.IsUnbound
                ? SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, contractDefinition)
                : SymbolEqualityComparer.Default.Equals(iface, contract);

            if (!matches)
            {
                continue;
            }

            if (candidateIsOpenGeneric && !ServiceTypeResolver.IsExactMatchOpenGenericServiceType(candidate, iface))
            {
                continue;
            }

            // The matched instantiation can carry type arguments the contract itself never named
            // (e.g. `IHandler<PrivateNested, int>` for `typeof(IHandler<,>)`), so its accessibility
            // has to be re-checked here rather than being covered by SSAL025 on the declaration.
            if (!TypeAccessibilityChecker.IsAccessible(iface, compilation))
            {
                continue;
            }

            builder ??= ImmutableArray.CreateBuilder<ConventionServiceTypeMatch>();
            builder.Add(candidateIsOpenGeneric
                ? new ConventionServiceTypeMatch(OpenGenericTypeofFormatter.Format(iface), IsOpenGeneric: true)
                : new ConventionServiceTypeMatch(SymbolFacts.ToFqn(iface), IsOpenGeneric: false));
        }

        if (builder is null)
        {
            return ImmutableArray<ConventionServiceTypeMatch>.Empty;
        }

        return builder
            .OrderBy(match => match.ServiceTypeFqn, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// The fully-qualified spelling of <paramref name="candidate"/> as the emitter must render it:
    /// typeof-form for an open generic class (whose own type parameter names do not exist in the
    /// generated extension method's scope), ordinary fully-qualified form otherwise.
    /// </summary>
    public static string GetImplementationTypeFqn(INamedTypeSymbol candidate) =>
        candidate.Arity > 0
            ? OpenGenericTypeofFormatter.Format(candidate)
            : SymbolFacts.ToFqn(candidate);

    private static bool HasServiceAttribute(INamedTypeSymbol type, INamedTypeSymbol? serviceAttributeSymbol)
    {
        if (serviceAttributeSymbol is null)
        {
            return false;
        }

        foreach (var attributeData in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, serviceAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
