using Microsoft.CodeAnalysis;
using SsalKit.StableHashing.Generator.Diagnostics;
using SsalKit.StableHashing.Generator.Models;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Classifies a member's (or a collection element's) type against the v1 encoding table (design
/// §4.4), recursively unwrapping <c>Nullable&lt;T&gt;</c>, nullable-annotated reference types, and
/// the four supported collection forms.
/// </summary>
/// <remarks>
/// Named-BCL-type recognition (<see cref="System.Guid"/>, <see cref="System.DateOnly"/>, etc.) is
/// done by namespace + type name rather than <see cref="ITypeSymbol.SpecialType"/>: those types
/// are not <see cref="SpecialType"/> members in every Roslyn version this generator's pinned
/// <c>Microsoft.CodeAnalysis.CSharp</c> floor supports, so a string match is the portable choice.
/// The eight numeric scalars, <see langword="bool"/>, <see langword="char"/>,
/// <see langword="decimal"/>, and <see langword="string"/> *do* use <see cref="SpecialType"/>:
/// those have been stable since Roslyn's first release.
/// </remarks>
internal static class TypeClassifier
{
    public static TypeClassification Classify(ITypeSymbol type) => ClassifyWithNullability(type);

    private static TypeClassification ClassifyWithNullability(ITypeSymbol type)
    {
        if (TryGetNullableValueUnderlyingType(type, out var underlying))
        {
            var inner = ClassifyCore(underlying);
            return inner.IsError
                ? inner
                : TypeClassification.Ok(new TypeShape(TypeShapeKind.NullableValue, null, null, null, inner.Shape, null, null));
        }

        if (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            var inner = ClassifyCore(type);
            return inner.IsError
                ? inner
                : TypeClassification.Ok(new TypeShape(TypeShapeKind.NullableReference, null, null, null, inner.Shape, null, null));
        }

        return ClassifyCore(type);
    }

    private static TypeClassification ClassifyCore(ITypeSymbol type)
    {
        if (IsNamed(type, "System", "DateTime"))
        {
            return TypeClassification.Error(DiagnosticDescriptors.DateTimeNotSupported);
        }

        if (IsNamed(type, "System", "Guid")) return Primitive("Guid");
        if (IsNamed(type, "System", "DateOnly")) return Primitive("DateOnly");
        if (IsNamed(type, "System", "TimeOnly")) return Primitive("TimeOnly");
        if (IsNamed(type, "System", "TimeSpan")) return Primitive("TimeSpan");
        if (IsNamed(type, "System", "DateTimeOffset")) return Primitive("DateTimeOffset");
        if (IsNamed(type, "System", "Int128")) return Primitive("Int128");
        if (IsNamed(type, "System", "UInt128")) return Primitive("UInt128");

        var scalarSuffix = ScalarAppendSuffix(type.SpecialType);
        if (scalarSuffix is not null)
        {
            return Primitive(scalarSuffix);
        }

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
        {
            var underlyingSpecialType = enumType.EnumUnderlyingType?.SpecialType ?? SpecialType.None;
            var underlyingSuffix = ScalarAppendSuffix(underlyingSpecialType);
            var underlyingKeyword = IntegralKeyword(underlyingSpecialType);

            if (underlyingSuffix is null || underlyingKeyword is null)
            {
                // Every C# enum's underlying type is one of the eight integral types mapped by
                // IntegralKeyword, so this defensive branch is not reachable in practice.
                return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
            }

            return TypeClassification.Ok(
                new TypeShape(TypeShapeKind.Enum, underlyingSuffix, underlyingKeyword, null, null, null, null));
        }

        if (type.TypeKind == TypeKind.Dynamic)
        {
            return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, "dynamic");
        }

        if (type.SpecialType == SpecialType.System_Object)
        {
            return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, "object");
        }

        if (type is IArrayTypeSymbol array)
        {
            if (array.Rank != 1)
            {
                return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
            }

            var elementClassification = ClassifyWithNullability(array.ElementType);
            if (elementClassification.IsError)
            {
                return elementClassification;
            }

            return TypeClassification.Ok(
                new TypeShape(TypeShapeKind.Collection, null, null, null, null, CollectionForm.Array, elementClassification.Shape));
        }

        if (type is INamedTypeSymbol named)
        {
            // The four supported collection forms and [StableHashContract] types are checked
            // *before* the general delegate/pointer/interface/abstract rejection below, because
            // IReadOnlyList<T> is itself a TypeKind.Interface: the general rejection would
            // otherwise shadow it (and any interface a contract type happens to declare) before
            // its specific, supported form is ever recognized.
            if (CollectionShapes.TryGetGenericForm(named, out var genericElementType, out var form))
            {
                var elementClassification = ClassifyWithNullability(genericElementType);
                if (elementClassification.IsError)
                {
                    return elementClassification;
                }

                return TypeClassification.Ok(
                    new TypeShape(TypeShapeKind.Collection, null, null, null, null, form, elementClassification.Shape));
            }

            if (ContractAttributeInfo.HasContractAttribute(named))
            {
                return TypeClassification.Ok(
                    new TypeShape(
                        TypeShapeKind.Contract,
                        null,
                        null,
                        ContractNaming.BuildExtensionsFqn(named),
                        null,
                        null,
                        null,
                        ContractTypeFqn: SsalKit.Generators.Toolkit.SymbolFacts.ToFqn(named)));
            }

            if (named.TypeKind is TypeKind.Delegate or TypeKind.Interface)
            {
                return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
            }

            if (named.TypeKind == TypeKind.Class && named.IsAbstract)
            {
                return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
            }

            if (ImplementsUnsupportedEnumerable(named))
            {
                return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
            }

            return TypeClassification.Error(DiagnosticDescriptors.MemberTypeHasNoContract, type.ToDisplayString());
        }

        // Pointers/function pointers are not INamedTypeSymbol, so they fall through to here; type
        // parameters and anything else not covered above land here too.
        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
        {
            return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
        }

        return TypeClassification.Error(DiagnosticDescriptors.UnsupportedMemberType, type.ToDisplayString());
    }

    private static TypeClassification Primitive(string appendMethodSuffix) =>
        TypeClassification.Ok(new TypeShape(TypeShapeKind.Primitive, appendMethodSuffix, null, null, null, null, null));

    private static bool TryGetNullableValueUnderlyingType(ITypeSymbol type, out ITypeSymbol underlyingType)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            underlyingType = named.TypeArguments[0];
            return true;
        }

        underlyingType = null!;
        return false;
    }

    /// <summary>
    /// Rejects <c>Dictionary&lt;,&gt;</c>, <c>HashSet&lt;&gt;</c>, and any other type that
    /// implements <see cref="System.Collections.Generic.IEnumerable{T}"/> or
    /// <see cref="System.Collections.IEnumerable"/> without being one of the four supported
    /// collection forms (design §4.4's "unordered or arbitrary IEnumerable&lt;T&gt;" rejection).
    /// </summary>
    private static bool ImplementsUnsupportedEnumerable(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                || iface.SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNamed(ITypeSymbol type, string ns, string name) =>
        type is INamedTypeSymbol { Arity: 0 } named
        && named.Name == name
        && SsalKit.Generators.Toolkit.SymbolFacts.GetContainingNamespaceName(named) == ns;

    private static string? ScalarAppendSuffix(SpecialType specialType) => specialType switch
    {
        SpecialType.System_Boolean => "Boolean",
        SpecialType.System_Char => "Char",
        SpecialType.System_SByte => "SByte",
        SpecialType.System_Byte => "Byte",
        SpecialType.System_Int16 => "Int16",
        SpecialType.System_UInt16 => "UInt16",
        SpecialType.System_Int32 => "Int32",
        SpecialType.System_UInt32 => "UInt32",
        SpecialType.System_Int64 => "Int64",
        SpecialType.System_UInt64 => "UInt64",
        SpecialType.System_Single => "Single",
        SpecialType.System_Double => "Double",
        SpecialType.System_Decimal => "Decimal",
        SpecialType.System_String => "String",
        _ => null,
    };

    private static string? IntegralKeyword(SpecialType specialType) => specialType switch
    {
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Int16 => "short",
        SpecialType.System_UInt16 => "ushort",
        SpecialType.System_Int32 => "int",
        SpecialType.System_UInt32 => "uint",
        SpecialType.System_Int64 => "long",
        SpecialType.System_UInt64 => "ulong",
        _ => null,
    };
}
