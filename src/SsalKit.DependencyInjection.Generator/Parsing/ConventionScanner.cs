using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Performs the whole-compilation convention scan declared by
/// <c>[assembly: RegisterImplementationsOf]</c>, turning it into the equatable
/// <see cref="ConventionRegistrationModel"/>s the emitter consumes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Incrementality.</strong> Unlike every other stage of the generator, this one cannot be
/// driven by a per-syntax-node provider: which classes a contract matches is a property of the
/// whole compilation, not of any one file, so the scan is a single node fed by
/// <c>CompilationProvider</c> and therefore re-runs on every compilation change. Two things keep
/// that affordable. First, <see cref="Scan"/> returns in constant time -- one metadata-name lookup,
/// and at most one pass over the assembly's own attribute list -- for a compilation that does not
/// use the feature, which is every existing consumer. Second, its result is a fully equatable model
/// array, so a compilation change that does not alter what the scan finds produces an equal value
/// and the downstream combine/emit stages are skipped entirely; the cost of an unrelated keystroke
/// is the scan itself, not a regenerated file.
/// </para>
/// <para>
/// The scan walks only <c>compilation.Assembly.GlobalNamespace</c>, which is exactly the documented
/// scope: types declared in the compilation being built, never ones from referenced assemblies.
/// </para>
/// </remarks>
internal static class ConventionScanner
{
    /// <summary>
    /// Returns one model per registration the compilation's convention scans produce, in the
    /// deterministic order the emitter writes them: by declared contract, then by implementation
    /// type, then by service type -- all ordinal. Grouping by contract first keeps every
    /// registration a single declaration produced together in the generated file, and sorting
    /// rather than using declaration order means reordering the <c>[assembly: ...]</c> lines does
    /// not churn the output.
    /// </summary>
    public static EquatableArray<ConventionRegistrationModel> Scan(Compilation compilation, CancellationToken cancellationToken)
    {
        var declarations = ContractDeclarationReader.Read(compilation);
        if (declarations.IsEmpty)
        {
            return EquatableArray<ConventionRegistrationModel>.Empty;
        }

        var validDeclarations = declarations
            .Where(declaration => declaration.Kind == ContractValidationKind.Valid)
            .ToImmutableArray();

        if (validDeclarations.IsEmpty)
        {
            return EquatableArray<ConventionRegistrationModel>.Empty;
        }

        var serviceAttributeSymbol = compilation.GetTypeByMetadataName(ContractDeclarationReader.ServiceAttributeMetadataName);

        var registrations = ImmutableArray.CreateBuilder<ConventionRegistrationModel>();

        foreach (var candidate in EnumerateAssemblyTypes(compilation, cancellationToken))
        {
            if (!ConventionImplementationMatcher.IsCandidate(candidate, serviceAttributeSymbol, compilation))
            {
                continue;
            }

            var implementationTypeFqn = ConventionImplementationMatcher.GetImplementationTypeFqn(candidate);

            foreach (var declaration in validDeclarations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var match in ConventionImplementationMatcher.Match(candidate, declaration, compilation))
                {
                    registrations.Add(new ConventionRegistrationModel(
                        declaration.ContractFqn,
                        match.ServiceTypeFqn,
                        implementationTypeFqn,
                        declaration.Lifetime,
                        declaration.Mode,
                        match.IsOpenGeneric));
                }
            }
        }

        return Order(registrations).ToEquatableArray();
    }

    /// <summary>
    /// Sorts the collected registrations deterministically and collapses exact duplicates.
    /// </summary>
    /// <remarks>
    /// Two overlapping contracts -- an unbound <c>typeof(IHandler&lt;&gt;)</c> alongside a closed
    /// <c>typeof(IHandler&lt;int&gt;)</c>, say -- can match the same class under the same service
    /// type. When they also agree on lifetime and mode, the two registrations are literally the
    /// same statement and emitting it twice would be pure noise, so the duplicate is dropped
    /// silently. When they disagree, both are kept (and <c>SSAL026</c> reports the ambiguity);
    /// dropping one would mean the generator silently picking a winner the declarations never
    /// expressed.
    /// </remarks>
    private static IEnumerable<ConventionRegistrationModel> Order(
        ImmutableArray<ConventionRegistrationModel>.Builder registrations)
    {
        var ordered = registrations
            .OrderBy(registration => registration.ContractFqn, StringComparer.Ordinal)
            .ThenBy(registration => registration.ImplementationTypeFqn, StringComparer.Ordinal)
            .ThenBy(registration => registration.ServiceTypeFqn, StringComparer.Ordinal)
            .ThenBy(registration => registration.Lifetime)
            .ThenBy(registration => registration.Mode);

        // Deduplication is on the *emitted* identity, which does not include ContractFqn -- that is
        // the whole point, since the duplicates worth collapsing come from two different contracts
        // matching the same class the same way. Ordering first is what makes "the first one wins"
        // deterministic: the surviving statement is always the one from the lexicographically
        // smallest contract.
        var emitted = new HashSet<(string ServiceTypeFqn, string ImplementationTypeFqn, int Lifetime, int Mode, bool IsOpenGeneric)>();

        foreach (var registration in ordered)
        {
            var identity = (
                registration.ServiceTypeFqn,
                registration.ImplementationTypeFqn,
                registration.Lifetime,
                registration.Mode,
                registration.IsOpenGeneric);

            if (emitted.Add(identity))
            {
                yield return registration;
            }
        }
    }

    /// <summary>
    /// Every named type declared in the compilation's own assembly, nested types included.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateAssemblyTypes(Compilation compilation, CancellationToken cancellationToken)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(compilation.Assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var member in stack.Pop().GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol childNamespace:
                        stack.Push(childNamespace);
                        break;

                    case INamedTypeSymbol type:
                        stack.Push(type);
                        yield return type;
                        break;
                }
            }
        }
    }
}
