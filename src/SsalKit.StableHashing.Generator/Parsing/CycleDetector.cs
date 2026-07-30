using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Detects whether following <c>[StableHashMember]</c> members' <c>[StableHashContract]</c> types
/// from a given contract, arbitrarily deep through nested contracts, collections, and nullable
/// wrappers, ever reaches that same contract again (SSALH005).
/// </summary>
/// <remarks>
/// This walks <see cref="ISymbol"/>s directly rather than going through
/// <see cref="TypeClassifier"/>'s <c>TypeShape</c> models: it needs to look *inside* nested
/// contract types (at their own <c>[StableHashMember]</c> members) to find a path back to the
/// root, which is more than any single contract's own emission model captures. Reachable member
/// types are resolved through <see cref="AttributeData"/>, which is available for a symbol
/// regardless of whether it was declared in this compilation or referenced from a metadata
/// assembly, so a cycle that passes through an external assembly's contract is still found.
/// </remarks>
internal static class CycleDetector
{
    // A defensive cap on the number of distinct contract types visited, in case a compilation
    // somehow contains an enormous or pathological contract graph. Real contract graphs are
    // orders of magnitude smaller; this exists only to bound worst-case work.
    private const int MaxVisitedTypes = 4096;

    /// <summary>
    /// Returns <see langword="true"/> when a path exists from <paramref name="root"/>, through one
    /// or more <c>[StableHashMember]</c>-decorated members' contract types, back to
    /// <paramref name="root"/> itself.
    /// </summary>
    public static bool HasCycle(INamedTypeSymbol root)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        return Search(root, root, visited);
    }

    /// <remarks>
    /// <paramref name="visited"/> is a permanent "fully explored, no path to root from here"
    /// memo, not a recursion-stack. That is sound specifically because the only question ever
    /// asked of a subtree is "does it reach <paramref name="root"/>": revisiting an
    /// already-explored node via a different path can never change that answer, so skipping it is
    /// both a valid optimization (it keeps a diamond-shaped graph from being explored
    /// exponentially) and what makes termination possible at all when the graph contains a cycle
    /// that does *not* involve <paramref name="root"/> (e.g. B -&gt; C -&gt; B, reached from
    /// root -&gt; B): without memoization that subtree would be walked forever.
    /// </remarks>
    private static bool Search(INamedTypeSymbol root, INamedTypeSymbol current, HashSet<INamedTypeSymbol> visited)
    {
        if (visited.Count > MaxVisitedTypes || !visited.Add(current))
        {
            return false;
        }

        foreach (var member in current.GetMembers())
        {
            if (member is not (IFieldSymbol or IPropertySymbol) || !ContractAttributeInfo.HasMemberAttribute(member))
            {
                continue;
            }

            var memberType = member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;

            foreach (var referenced in ExtractContractReferences(memberType))
            {
                if (SymbolEqualityComparer.Default.Equals(referenced, root))
                {
                    return true;
                }

                if (Search(root, referenced, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Unwraps <see cref="IArrayTypeSymbol"/>, <c>Nullable&lt;T&gt;</c>, and the four supported
    /// collection forms to find every directly- or indirectly-referenced
    /// <c>[StableHashContract]</c> type reachable from <paramref name="type"/>. Does not otherwise
    /// validate <paramref name="type"/> -- an unsupported member type simply yields no reference
    /// here and is reported separately by <see cref="TypeClassifier"/>.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> ExtractContractReferences(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            foreach (var reference in ExtractContractReferences(array.ElementType))
            {
                yield return reference;
            }

            yield break;
        }

        if (type is not INamedTypeSymbol named)
        {
            yield break;
        }

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            foreach (var reference in ExtractContractReferences(named.TypeArguments[0]))
            {
                yield return reference;
            }

            yield break;
        }

        if (ContractAttributeInfo.HasContractAttribute(named))
        {
            yield return named;
            yield break;
        }

        if (CollectionShapes.TryGetElementType(named, out var elementType))
        {
            foreach (var reference in ExtractContractReferences(elementType))
            {
                yield return reference;
            }
        }
    }
}
