using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// The symbol-level questions both parsers ask, in one place: how deep an exception sits, how a
/// type has to be re-declared in a generated partial, and how an enum value has to be written back
/// out as C#.
/// </summary>
internal static class SymbolFacts
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
    /// Returns <see langword="true"/> when <paramref name="type"/> has type parameters of its own or
    /// is nested inside a type that does.
    /// </summary>
    public static bool IsGenericOrNestedInGeneric(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> and every type containing it are
    /// public, which is the only case where a generated member may expose it and still be declared
    /// <see langword="public"/> itself.
    /// </summary>
    public static bool IsEffectivelyPublic(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the clause naming why <paramref name="type"/> cannot be named from a generated file
    /// in the same assembly, or <see langword="null"/> when it can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole nesting chain is walked, because a public type nested in a private one is no more
    /// reachable than a private one. "Reachable" here means <c>internal</c> or wider at every step:
    /// the generated file is a separate file of the same assembly, so it sees everything the
    /// assembly sees, but nothing that a type's own declaration keeps to itself
    /// (<c>private</c>/<c>protected</c>/<c>private protected</c>) and nothing that another file
    /// keeps to itself (a <c>file</c>-local type, which is <c>internal</c> as far as
    /// <see cref="ISymbol.DeclaredAccessibility"/> is concerned and therefore has to be asked about
    /// separately).
    /// </para>
    /// <para>
    /// This is deliberately a property of the type alone rather than of the type and the container
    /// that would name it: an exception nested privately inside its own container would in fact be
    /// reachable from that one container's generated part, but making the rule depend on where the
    /// exception happens to be registered would mean the same declaration is legal or illegal
    /// depending on a second file.
    /// </para>
    /// </remarks>
    public static string? GetInaccessibleReason(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            var isSelf = ReferenceEquals(current, type);

            if (current.IsFileLocal)
            {
                return isSelf
                    ? "it is a file-local type"
                    : "it is nested inside the file-local type '" + current.ToDisplayString() + "'";
            }

            if (!IsAtLeastInternal(current.DeclaredAccessibility))
            {
                var keyword = ToAccessibilityKeyword(current.DeclaredAccessibility);

                return isSelf
                    ? "it is declared '" + keyword + "'"
                    : "it is nested inside '" + current.ToDisplayString() + "', which is declared '" + keyword + "'";
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the accessibility lets any other file of the same assembly name the type.
    /// <c>protected internal</c> qualifies (it is <i>internal or</i> protected), <c>private
    /// protected</c> does not (it is <i>internal and</i> protected).
    /// </summary>
    private static bool IsAtLeastInternal(Accessibility accessibility) =>
        accessibility is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal
            or Accessibility.NotApplicable;

    /// <summary>
    /// The <c>global::</c>-prefixed fully qualified name, which is how every type reference in the
    /// generated code is written.
    /// </summary>
    public static string ToFqn(ISymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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

        var containingNamespace = type.ContainingNamespace;
        if (containingNamespace is not null && !containingNamespace.IsGlobalNamespace)
        {
            segments.AddRange(Enumerable.Reverse(containingNamespace.ToDisplayString().Split('.')));
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
        var enumFqn = ToFqn(codeEnum);

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
        var enumFqn = ToFqn(codeEnum);

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
