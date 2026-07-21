using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Analysis;

/// <summary>
/// Normalizes a <c>typeof(...)</c> <c>Key</c> type symbol so that two spellings which produce the
/// exact same runtime <see cref="System.Type"/> also produce the exact same identity string for
/// <see cref="ServiceAttributeAnalyzer"/>'s SSAL004 duplicate-key detection.
/// </summary>
/// <remarks>
/// This is used *only* for SSAL004 duplicate-key comparison, never for the generated source text:
/// the source-level spelling captured by <c>KeyLiteralFormatter</c> is left untouched there, since
/// it already compiles to the correct runtime value regardless of which of two equivalent spellings
/// was used. Two kinds of source-level spelling differ while being runtime-identical:
/// <list type="bullet">
/// <item>Named vs. unnamed tuple elements -- <c>(int A, string B)</c> and <c>(int, string)</c> are
/// the same <c>System.ValueTuple&lt;int, string&gt;</c> at runtime; element names are an
/// annotation understood only by the C# compiler and are erased entirely by the time
/// <c>typeof(...)</c> observes them.</item>
/// <item><c>nint</c>/<c>nuint</c> vs. <c>System.IntPtr</c>/<c>System.UIntPtr</c> -- the "native
/// int" types are compile-time-only spellings of the corresponding classic BCL type.</item>
/// </list>
/// <see cref="SymbolEqualityComparer.Default"/> is not suitable for this on its own: it treats
/// differently-named tuples as unequal, matching ordinary type-equality semantics rather than
/// runtime identity.
/// </remarks>
internal static class KeyIdentityNormalizer
{
    /// <summary>
    /// Returns a fully-qualified display string for <paramref name="type"/> with every tuple
    /// (nested arbitrarily deep inside arrays, pointers, and generic type arguments) rewritten to
    /// its unnamed <c>ValueTuple&lt;...&gt;</c> form, and every native integer type rewritten to
    /// its classic <c>IntPtr</c>/<c>UIntPtr</c> form.
    /// </summary>
    public static string GetNormalizedIdentity(ITypeSymbol type, Compilation compilation) =>
        Normalize(type, compilation).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static ITypeSymbol Normalize(ITypeSymbol type, Compilation compilation)
    {
        return type switch
        {
            IArrayTypeSymbol arrayType => compilation.CreateArrayTypeSymbol(Normalize(arrayType.ElementType, compilation), arrayType.Rank),
            IPointerTypeSymbol pointerType => compilation.CreatePointerTypeSymbol(Normalize(pointerType.PointedAtType, compilation)),
            INamedTypeSymbol namedType => NormalizeNamedType(namedType, compilation),
            // Function pointers can't reach here via a `[Service]` Key (typeof(delegate*<...>) is
            // CS8911), type parameters are never a resolved Key type, and anything else (dynamic,
            // etc.) has no tuple/native-int structure to normalize -- returned unchanged.
            _ => type,
        };
    }

    private static ITypeSymbol NormalizeNamedType(INamedTypeSymbol type, Compilation compilation)
    {
        if (type.IsTupleType && type.TupleUnderlyingType is { } tupleUnderlyingType)
        {
            // Recurse rather than just using tupleUnderlyingType directly: its own type arguments
            // are the tuple's element types, which may themselves be (nested) tuples or nint/nuint.
            return NormalizeNamedType(tupleUnderlyingType, compilation);
        }

        if (type.IsNativeIntegerType && type.NativeIntegerUnderlyingType is { } nativeIntegerUnderlyingType)
        {
            // The underlying type (System.IntPtr/System.UIntPtr) has no type arguments of its own,
            // so there is nothing further to normalize -- return it directly.
            return nativeIntegerUnderlyingType;
        }

        if (type.Arity == 0 || type.TypeArguments.IsDefaultOrEmpty)
        {
            return type;
        }

        // Reconstruct with normalized type arguments, keeping everything else (containing type,
        // namespace) exactly as Roslyn already represents it -- this correctly handles nested
        // generic containing types without any manual string surgery.
        var normalizedArguments = type.TypeArguments.Select(argument => Normalize(argument, compilation)).ToArray();
        return type.ConstructedFrom.Construct(normalizedArguments);
    }
}
