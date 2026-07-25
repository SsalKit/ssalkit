using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// The Guard-specific symbol-level questions both parsers ask, in one place: how deep an exception
/// sits, how a type has to be re-declared in a generated partial, and how an enum value has to be
/// written back out as C#.
/// </summary>
/// <remarks>
/// The generator-agnostic half of this -- fully qualified names, the effective-public test, the
/// generic test, and the generated-code accessibility walk -- lives in
/// <see cref="SymbolFacts"/> in SsalKit.Generators.Toolkit; only the parts that encode a Guard
/// rule or a Guard message are here. The type is named <c>GuardSymbolFacts</c> rather than
/// <c>SymbolFacts</c> precisely so that the toolkit's type stays reachable by its own unqualified
/// name from this namespace.
/// </remarks>
internal static class GuardSymbolFacts
{
    /// <summary>
    /// The metadata name of the base class every <c>[ErrorCode]</c> exception must derive from.
    /// </summary>
    public const string ErrorCodedExceptionMetadataName = "SsalKit.Guard.ErrorCodedException";

    /// <summary>
    /// Returns the number of base-type steps from <paramref name="type"/> to
    /// <paramref name="exceptionType"/> (0 for <c>System.Exception</c> itself, 1 for a type deriving
    /// directly from it), or <see langword="null"/> when <paramref name="type"/> is not an exception
    /// at all.
    /// </summary>
    /// <remarks>
    /// This is the mapping table's sort key. Measuring the distance to <c>System.Exception</c> rather
    /// than comparing pairs of registrations keeps the sort a plain, total, deterministic ordering:
    /// a derived type is always strictly deeper than its base, which is all the
    /// derived-before-base guarantee needs, and unrelated types fall wherever the tiebreak puts
    /// them.
    /// </remarks>
    public static int? GetExceptionDepth(INamedTypeSymbol type, INamedTypeSymbol? exceptionType)
    {
        if (exceptionType is null)
        {
            return null;
        }

        var depth = 0;
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, exceptionType))
            {
                return depth;
            }

            depth++;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> derives from
    /// <paramref name="baseType"/> (or is it).
    /// </summary>
    public static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
        {
            return false;
        }

        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the clause naming why <paramref name="type"/> cannot be named from a generated file
    /// in the same assembly, or <see langword="null"/> when it can.
    /// </summary>
    /// <remarks>
    /// The walk itself is <see cref="SymbolFacts.FindGeneratedCodeAccessBlocker"/>; what is Guard's
    /// own is only the wording, which names the offending declaration and says whether it is the
    /// registered type itself or one of its containers -- the difference between a message a user
    /// can act on and one that just says "no".
    /// </remarks>
    public static string? GetInaccessibleReason(INamedTypeSymbol type)
    {
        var blocker = SymbolFacts.FindGeneratedCodeAccessBlocker(type);
        if (blocker is null)
        {
            return null;
        }

        var isSelf = ReferenceEquals(blocker, type);

        if (blocker.IsFileLocal)
        {
            return isSelf
                ? "it is a file-local type"
                : "it is nested inside the file-local type '" + blocker.ToDisplayString() + "'";
        }

        var keyword = ToAccessibilityKeyword(blocker.DeclaredAccessibility);

        return isSelf
            ? "it is declared '" + keyword + "'"
            : "it is nested inside '" + blocker.ToDisplayString() + "', which is declared '" + keyword + "'";
    }

    /// <summary>
    /// The type's fully qualified name flattened into a single identifier
    /// (<c>Game_Loot_UserNotFoundException</c>). Used as the last-resort helper name: fully
    /// qualified names are unique within a compilation, so this can never collide.
    /// </summary>
    public static string ToFlattenedIdentifier(INamedTypeSymbol type)
    {
        var segments = new List<string>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            segments.Add(current.Name);
        }

        var containingNamespace = SymbolFacts.GetContainingNamespaceName(type);
        if (containingNamespace.Length > 0)
        {
            segments.AddRange(Enumerable.Reverse(containingNamespace.Split('.')));
        }

        segments.Reverse();
        return CSharpNaming.JoinIdentifierSegments(segments);
    }

    /// <summary>
    /// Whether the type's declaration in source carries the <c>partial</c> modifier.
    /// </summary>
    public static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    /// <summary>
    /// Re-declares <paramref name="type"/> the way the generated part must, e.g.
    /// <c>public static partial class GameErrors</c>.
    /// </summary>
    /// <remarks>
    /// The accessibility is always written out explicitly rather than left to the default, so the
    /// generated part can never disagree with the hand-written one. <c>sealed</c>, <c>abstract</c>
    /// and the rest are deliberately omitted: a partial type takes those from whichever part
    /// declares them, and repeating them would only create a second place to get them wrong.
    /// </remarks>
    public static string ToPartialDeclaration(INamedTypeSymbol type)
    {
        var keyword = type.TypeKind switch
        {
            TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
            TypeKind.Interface => "interface",
            _ => type.IsRecord ? "record" : "class",
        };

        var staticModifier = type.IsStatic ? "static " : string.Empty;

        return ToAccessibilityKeyword(type.DeclaredAccessibility) + " " + staticModifier + "partial " + keyword + " " + type.Name;
    }

    /// <summary>
    /// Writes an enum value the way generated C# has to reference it: by member name when one
    /// matches, and as a cast of the underlying constant when none does (a combination of
    /// <c>[Flags]</c> members, or a value cast into the enum at the attribute site).
    /// </summary>
    public static string ToCodeExpression(TypedConstant code, INamedTypeSymbol codeEnum)
    {
        var enumFqn = SymbolFacts.ToFqn(codeEnum);

        if (code.Value is null)
        {
            return "default(" + enumFqn + ")";
        }

        var memberName = codeEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue && Equals(field.ConstantValue, code.Value))
            .Select(field => field.Name)
            .OrderBy(name => name, System.StringComparer.Ordinal)
            .FirstOrDefault();

        return memberName is null
            ? "(" + enumFqn + ")(" + ToNumericLiteral(code.Value, codeEnum.EnumUnderlyingType) + ")"
            : enumFqn + "." + memberName;
    }

    /// <summary>
    /// How documentation refers to a code: <c>GameStatusCode.UserNotFound</c>, or the cast form when
    /// the value names no member.
    /// </summary>
    public static string ToCodeDisplayName(string codeExpression, INamedTypeSymbol codeEnum)
    {
        var enumFqn = SymbolFacts.ToFqn(codeEnum);

        return codeExpression.StartsWith(enumFqn + ".", System.StringComparison.Ordinal)
            ? codeEnum.Name + codeExpression.Substring(enumFqn.Length)
            : codeExpression.Replace(enumFqn, codeEnum.Name);
    }

    private static string ToNumericLiteral(object value, INamedTypeSymbol? underlyingType)
    {
        var literal = System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";

        // An enum's underlying constant is written back with the suffix its own type needs, so a
        // value outside int range still compiles where the cast is emitted.
        return underlyingType?.SpecialType switch
        {
            SpecialType.System_UInt32 => literal + "U",
            SpecialType.System_Int64 => literal + "L",
            SpecialType.System_UInt64 => literal + "UL",
            _ => literal,
        };
    }

    private static string ToAccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Private => "private",
        _ => "internal",
    };
}
