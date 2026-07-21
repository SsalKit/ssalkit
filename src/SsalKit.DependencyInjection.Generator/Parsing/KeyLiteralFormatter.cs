using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Converts the <c>Key</c> argument of a <c>[Service]</c> attribute (a <see cref="TypedConstant"/>
/// captured from attribute metadata) into a self-contained, fully-qualified C# expression that can
/// be embedded verbatim into generated source and reproduces the exact same value.
/// </summary>
internal static class KeyLiteralFormatter
{
    /// <summary>
    /// Formats <paramref name="constant"/> as a C# literal/expression, or <see langword="null"/>
    /// if the constant does not represent a supported, non-null key value.
    /// </summary>
    public static string? Format(TypedConstant constant)
    {
        if (constant.IsNull || constant.Kind == TypedConstantKind.Error)
        {
            return null;
        }

        return constant.Kind switch
        {
            TypedConstantKind.Enum => FormatEnum(constant),
            TypedConstantKind.Primitive => FormatPrimitive(constant),
            TypedConstantKind.Type => FormatType(constant),
            _ => null,
        };
    }

    private static string? FormatType(TypedConstant constant)
    {
        // Key = typeof(SomeType), including open/unbound generic type definitions such as
        // typeof(List<>); FullyQualifiedFormat renders both correctly and unambiguously.
        return constant.Value is ITypeSymbol typeSymbol
            ? $"typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})"
            : null;
    }

    private static string? FormatEnum(TypedConstant constant)
    {
        var enumType = constant.Type;
        if (enumType is null)
        {
            return null;
        }

        var enumTypeFqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field
                && Equals(field.ConstantValue, constant.Value))
            {
                return $"{enumTypeFqn}.{EscapeIfKeyword(field.Name)}";
            }
        }

        // No matching member (e.g. a combined [Flags] value, or a raw numeric cast). Fall back to
        // an explicit cast of the underlying integral value. TypedConstant.Value for an enum
        // constant is always boxed as its *underlying* integral type (byte/sbyte/short/ushort/
        // int/uint/long/ulong), matching IFieldSymbol.ConstantValue above, so switching on the
        // boxed value's runtime type -- rather than funneling everything through
        // Convert.ToInt64, which throws OverflowException for ulong values using the top bit --
        // always round-trips regardless of the enum's underlying type.
        var literal = constant.Value switch
        {
            byte value => $"(byte){value.ToString(CultureInfo.InvariantCulture)}",
            sbyte value => $"(sbyte){value.ToString(CultureInfo.InvariantCulture)}",
            short value => $"(short){value.ToString(CultureInfo.InvariantCulture)}",
            ushort value => $"(ushort){value.ToString(CultureInfo.InvariantCulture)}",
            int value => value.ToString(CultureInfo.InvariantCulture),
            uint value => $"{value.ToString(CultureInfo.InvariantCulture)}U",
            long value => $"{value.ToString(CultureInfo.InvariantCulture)}L",
            ulong value => $"{value.ToString(CultureInfo.InvariantCulture)}UL",
            _ => null,
        };

        return literal is null ? null : $"({enumTypeFqn})({literal})";
    }

    /// <summary>
    /// Prefixes <paramref name="identifier"/> with <c>@</c> if it is a reserved C# keyword (e.g. an
    /// enum member literally named <c>default</c>), so it can be used as a member-access identifier
    /// in generated source without producing a syntax error.
    /// </summary>
    private static string EscapeIfKeyword(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : $"@{identifier}";

    private static string? FormatPrimitive(TypedConstant constant)
    {
        var value = constant.Value;
        if (value is null)
        {
            return null;
        }

        return constant.Type?.SpecialType switch
        {
            SpecialType.System_String => SymbolDisplay.FormatLiteral((string)value, quote: true),
            SpecialType.System_Char => SymbolDisplay.FormatLiteral((char)value, quote: true),
            SpecialType.System_Boolean => (bool)value ? "true" : "false",
            SpecialType.System_Byte => $"(byte){((byte)value).ToString(CultureInfo.InvariantCulture)}",
            SpecialType.System_SByte => $"(sbyte){((sbyte)value).ToString(CultureInfo.InvariantCulture)}",
            SpecialType.System_Int16 => $"(short){((short)value).ToString(CultureInfo.InvariantCulture)}",
            SpecialType.System_UInt16 => $"(ushort){((ushort)value).ToString(CultureInfo.InvariantCulture)}",
            SpecialType.System_Int32 => ((int)value).ToString(CultureInfo.InvariantCulture),
            SpecialType.System_UInt32 => $"{((uint)value).ToString(CultureInfo.InvariantCulture)}U",
            SpecialType.System_Int64 => $"{((long)value).ToString(CultureInfo.InvariantCulture)}L",
            SpecialType.System_UInt64 => $"{((ulong)value).ToString(CultureInfo.InvariantCulture)}UL",
            SpecialType.System_Single => FormatSingle((float)value),
            SpecialType.System_Double => FormatDouble((double)value),
            // decimal is intentionally absent: it is not a legal attribute argument type, so a
            // decimal TypedConstant can never reach this method.
            _ => null,
        };
    }

    // "R"-formatting NaN/Infinity produces "NaN"/"Infinity"/"-Infinity", which combined with the
    // float/double suffix yields illegal C# tokens ("NaND", "InfinityF", ...). These three values
    // have no literal representation at all and must instead be spelled out as the corresponding
    // framework constant.
    private static string FormatSingle(float value)
    {
        if (float.IsNaN(value))
        {
            return "global::System.Single.NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "global::System.Single.PositiveInfinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "global::System.Single.NegativeInfinity";
        }

        return $"{value.ToString("R", CultureInfo.InvariantCulture)}F";
    }

    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return "global::System.Double.NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "global::System.Double.PositiveInfinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "global::System.Double.NegativeInfinity";
        }

        return $"{value.ToString("R", CultureInfo.InvariantCulture)}D";
    }
}
