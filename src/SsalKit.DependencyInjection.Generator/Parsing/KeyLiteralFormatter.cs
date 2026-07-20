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
            _ => null,
        };
    }

    private static string? FormatEnum(TypedConstant constant)
    {
        var enumType = constant.Type;
        if (enumType is null)
        {
            return null;
        }

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field
                && Equals(field.ConstantValue, constant.Value))
            {
                return $"{enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{field.Name}";
            }
        }

        // No matching member (e.g. a combined [Flags] value, or a raw numeric cast). Fall back to
        // an explicit cast of the underlying integral value, which always round-trips.
        var underlyingValue = System.Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture);
        return $"({enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})({underlyingValue.ToString(CultureInfo.InvariantCulture)})";
    }

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
            SpecialType.System_Single => $"{((float)value).ToString("R", CultureInfo.InvariantCulture)}F",
            SpecialType.System_Double => $"{((double)value).ToString("R", CultureInfo.InvariantCulture)}D",
            // decimal is intentionally absent: it is not a legal attribute argument type, so a
            // decimal TypedConstant can never reach this method.
            _ => null,
        };
    }
}
