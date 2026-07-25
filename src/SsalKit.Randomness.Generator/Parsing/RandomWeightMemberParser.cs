using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Randomness.Generator.Diagnostics;
using SsalKit.Randomness.Generator.Models;

namespace SsalKit.Randomness.Generator.Parsing;

/// <summary>
/// Turns one <c>[RandomWeight]</c>-decorated member into a <see cref="WeightedMemberModel"/>:
/// either the strings the emitter needs, or the member-level diagnostic that disqualified it.
/// </summary>
/// <remarks>
/// No <see cref="ISymbol"/>, <see cref="SyntaxNode"/>, or <see cref="Compilation"/> survives into
/// the returned model -- only strings, enums, and <see cref="LocationInfo"/> -- which is what lets
/// the incremental pipeline compare runs by value instead of re-emitting on every keystroke.
/// </remarks>
internal static class RandomWeightMemberParser
{
    private const string InternalExtensionsArgumentName = "InternalExtensions";
    private const string ExtensionClassSuffix = "RandomWeightExtensions";
    private const string HintNameSuffix = ".RandomWeight";
    private const string GlobalPrefix = "global::";

    public static WeightedMemberModel? GetModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var member = context.TargetSymbol;

        // [AttributeUsage] already limits the attribute to properties and fields, so anything else
        // reaching here would be a compiler error at the application site; there is nothing useful
        // to add on top of that error.
        if (member is not (IPropertySymbol or IFieldSymbol))
        {
            return null;
        }

        var declaringType = member.ContainingType;
        if (declaringType is null)
        {
            return null;
        }

        var location = LocationInfo.CreateFrom(GetReportLocation(context));
        var typeFqn = declaringType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var typeDisplayName = declaringType.ToDisplayString();
        var memberDisplayName = typeDisplayName + "." + member.Name;

        var diagnostic = Validate(declaringType, member, typeDisplayName, memberDisplayName, location, out var weightKind);
        if (diagnostic is not null)
        {
            return new WeightedMemberModel(typeFqn, typeDisplayName, member.Name, location, Type: null, diagnostic);
        }

        var typeModel = BuildTypeModel(context, declaringType, member, typeFqn, weightKind);
        return new WeightedMemberModel(typeFqn, typeDisplayName, member.Name, location, typeModel, Diagnostic: null);
    }

    /// <summary>
    /// Runs the member-level rules in a fixed order -- declaring type first (SSALR005/006/004),
    /// then the member itself (SSALR003/004/001) -- and reports at most one of them, so a member
    /// that breaks several rules produces the single most fundamental message rather than a pile.
    /// </summary>
    private static DiagnosticInfo? Validate(
        INamedTypeSymbol declaringType,
        ISymbol member,
        string typeDisplayName,
        string memberDisplayName,
        LocationInfo? location,
        out WeightKind weightKind)
    {
        weightKind = WeightKind.Integral;

        // SSALR005: an open generic declaring type has no single closed receiver type to write.
        if (IsGenericOrNestedInGeneric(declaringType))
        {
            return new DiagnosticInfo(DiagnosticDescriptors.GenericTypeNotSupported, location, memberDisplayName);
        }

        // SSALR006: a ref struct cannot be a generic type argument of IReadOnlyList<T>.
        if (declaringType.IsRefLikeType)
        {
            return new DiagnosticInfo(DiagnosticDescriptors.RefStructNotSupported, location, memberDisplayName);
        }

        // SSALR004: the declaring type has to be nameable from a sibling top-level class.
        if (!GeneratedCodeAccessibility.IsTypeVisible(declaringType))
        {
            return new DiagnosticInfo(
                DiagnosticDescriptors.InaccessibleWeightMember, location, memberDisplayName, "its declaring type '" + typeDisplayName + "'");
        }

        // SSALR003: the selector reads the member off an instance, so it must be readable per item.
        var invalidKindReason = GetInvalidMemberKindReason(member);
        if (invalidKindReason is not null)
        {
            return new DiagnosticInfo(DiagnosticDescriptors.InvalidWeightMemberKind, location, memberDisplayName, invalidKindReason);
        }

        // SSALR004: ... and the member itself has to be readable from there too.
        if (!GeneratedCodeAccessibility.IsMemberReadable(member))
        {
            return new DiagnosticInfo(DiagnosticDescriptors.InaccessibleWeightMember, location, memberDisplayName, "it");
        }

        // SSALR001: and its type must map onto one of the two runtime selector shapes.
        var memberType = GetMemberType(member);
        var kind = GetWeightKind(memberType);
        if (kind is null)
        {
            return new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedWeightType,
                location,
                memberDisplayName,
                memberType.ToDisplayString(),
                GetExcludedTypeNote(memberType));
        }

        weightKind = kind.Value;
        return null;
    }

    private static WeightedTypeModel BuildTypeModel(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol declaringType,
        ISymbol member,
        string typeFqn,
        WeightKind weightKind)
    {
        var containingNamespace = declaringType.ContainingNamespace;
        var namespaceName = containingNamespace is null || containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString();

        var isPublic = GeneratedCodeAccessibility.IsEffectivelyPublic(declaringType) && !ForcesInternalExtensions(context);

        return new WeightedTypeModel(
            namespaceName,
            typeFqn,
            BuildExtensionClassName(declaringType),
            CSharpNaming.EscapeKeyword(member.Name),
            weightKind,
            isPublic,
            BuildHintName(typeFqn));
    }

    /// <summary>
    /// Builds <c>Outer_InnerRandomWeightExtensions</c> for a nested type and
    /// <c>LootEntryRandomWeightExtensions</c> for a top-level one. Flattening (rather than nesting
    /// the generated class) keeps it a top-level type in the declaring type's namespace, which is
    /// what makes its extension methods usable without an extra <c>using</c>.
    /// </summary>
    private static string BuildExtensionClassName(INamedTypeSymbol declaringType)
    {
        var names = new List<string>();
        for (var current = declaringType; current is not null; current = current.ContainingType)
        {
            names.Add(current.Name);
        }

        names.Reverse();
        return string.Join("_", names) + ExtensionClassSuffix;
    }

    private static string BuildHintName(string typeFqn)
    {
        // HintNameSanitizer would turn the "global::" prefix's colons into underscores, which is
        // safe but noisy in every generated file name; the prefix carries no information here.
        var trimmed = typeFqn.StartsWith(GlobalPrefix, System.StringComparison.Ordinal)
            ? typeFqn.Substring(GlobalPrefix.Length)
            : typeFqn;

        return HintNameSanitizer.Sanitize(trimmed + HintNameSuffix);
    }

    private static bool ForcesInternalExtensions(GeneratorAttributeSyntaxContext context)
    {
        foreach (var attribute in context.Attributes)
        {
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == InternalExtensionsArgumentName && namedArgument.Value.Value is true)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsGenericOrNestedInGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the clause naming why the member cannot be read per item, or <see langword="null"/>
    /// when it can be.
    /// </summary>
    private static string? GetInvalidMemberKindReason(ISymbol member)
    {
        if (member.IsStatic)
        {
            return "static";
        }

        return member switch
        {
            IPropertySymbol { IsIndexer: true } => "an indexer",
            IPropertySymbol { GetMethod: null } => "a write-only property",
            _ => null,
        };
    }

    private static ITypeSymbol GetMemberType(ISymbol member) =>
        member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;

    private static WeightKind? GetWeightKind(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64 => WeightKind.Integral,
        SpecialType.System_Single or SpecialType.System_Double => WeightKind.Floating,
        _ => null,
    };

    /// <summary>
    /// Spells out why the two numeric types a user is most likely to reach for next are missing,
    /// so SSALR001 does not read as an oversight. Empty for every other unsupported type, where the
    /// list of supported types in the message is answer enough.
    /// </summary>
    private static string GetExcludedTypeNote(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_UInt64 =>
            "; 'ulong' is excluded deliberately because converting it to the runtime API's 'long' weight can overflow",
        SpecialType.System_Decimal =>
            "; 'decimal' is excluded deliberately because no weighted-picking overload accepts it",
        _ => string.Empty,
    };

    /// <summary>
    /// Reports on the attribute application itself, which is the token the user wrote and can
    /// delete. Falls back to the decorated node when the attribute's syntax cannot be recovered
    /// (never observed in practice for an attribute the transform was triggered by).
    /// </summary>
    private static Location? GetReportLocation(GeneratorAttributeSyntaxContext context)
    {
        var attributeSyntax = context.Attributes
            .Select(attribute => attribute.ApplicationSyntaxReference)
            .FirstOrDefault(reference => reference is not null);

        return attributeSyntax is null ? context.TargetNode.GetLocation() : attributeSyntax.GetSyntax().GetLocation();
    }
}
