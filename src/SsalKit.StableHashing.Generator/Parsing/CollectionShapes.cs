using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.StableHashing.Generator.Models;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Recognizes the four collection forms the v1 encoding contract supports (design §4.4):
/// <c>T[]</c>, <c>List&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>, and
/// <c>ImmutableArray&lt;T&gt;</c>. Shared by <see cref="TypeClassifier"/> (which needs the
/// element type and the form) and <see cref="CycleDetector"/> (which only needs the element
/// type, to keep looking for a nested <c>[StableHashContract]</c> reference).
/// </summary>
internal static class CollectionShapes
{
    /// <summary>
    /// Recognizes <c>List&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>, and
    /// <c>ImmutableArray&lt;T&gt;</c> (arrays are <see cref="IArrayTypeSymbol"/>, not
    /// <see cref="INamedTypeSymbol"/>, so callers check those separately).
    /// </summary>
    public static bool TryGetGenericForm(INamedTypeSymbol type, out ITypeSymbol elementType, out CollectionForm form)
    {
        var original = type.OriginalDefinition;

        if (IsGenericType(original, "System.Collections.Generic", "List`1"))
        {
            elementType = type.TypeArguments[0];
            form = CollectionForm.List;
            return true;
        }

        if (IsGenericType(original, "System.Collections.Generic", "IReadOnlyList`1"))
        {
            elementType = type.TypeArguments[0];
            form = CollectionForm.ReadOnlyList;
            return true;
        }

        if (IsGenericType(original, "System.Collections.Immutable", "ImmutableArray`1"))
        {
            elementType = type.TypeArguments[0];
            form = CollectionForm.ImmutableArray;
            return true;
        }

        elementType = null!;
        form = default;
        return false;
    }

    /// <summary>The element type alone, for callers (like cycle detection) that do not need the form.</summary>
    public static bool TryGetElementType(INamedTypeSymbol type, out ITypeSymbol elementType) =>
        TryGetGenericForm(type, out elementType, out _);

    private static bool IsGenericType(INamedTypeSymbol type, string ns, string metadataName) =>
        type.MetadataName == metadataName && SymbolFacts.GetContainingNamespaceName(type) == ns;
}
