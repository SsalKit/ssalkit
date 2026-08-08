using System.Collections.Generic;
using System.Collections.Immutable;
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
/// <para>
/// No <see cref="ISymbol"/>, <see cref="SyntaxNode"/>, or <see cref="Compilation"/> survives into
/// the returned model -- only strings, enums, and <see cref="LocationInfo"/> -- which is what lets
/// the incremental pipeline compare runs by value instead of re-emitting on every keystroke.
/// </para>
/// <para>
/// The rules live behind the symbol-level <see cref="GetModel(ISymbol, ImmutableArray{AttributeData}, Location, CancellationToken)"/>
/// overload rather than behind the <see cref="GeneratorAttributeSyntaxContext"/> one, because the
/// decorated member does not always come from an attribute transform: an attribute written with the
/// <c>property:</c> target lands on a symbol the attribute provider never reports (see
/// <see cref="TargetRedirectedRandomWeightParser"/>), and every rule here has to apply to it just
/// the same.
/// </para>
/// </remarks>
internal static class RandomWeightMemberParser
{
    private const string InternalExtensionsArgumentName = "InternalExtensions";
    private const string SharedSourceOverloadsArgumentName = "SharedSourceOverloads";
    private const string ExtensionClassSuffix = "RandomWeightExtensions";
    private const string HintNameSuffix = ".RandomWeight";

    /// <summary>
    /// Parses the member an attribute transform reported: the ordinary path, for an attribute written
    /// on a property or field declaration with no target specifier.
    /// </summary>
    /// <param name="context">The attribute transform's context.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The member model, or <see langword="null"/>; see the overload below.</returns>
    public static WeightedMemberModel? GetModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken) =>
        GetModel(context.TargetSymbol, context.Attributes, GetReportLocation(context), cancellationToken);

    /// <summary>
    /// Validates <paramref name="member"/> and, when it passes, builds its emission model.
    /// </summary>
    /// <param name="member">The decorated member: a property or a field.</param>
    /// <param name="attributes">
    /// The <c>[RandomWeight]</c> applications the named arguments are read from. All of them are
    /// consulted, so an argument written on any one application counts (the language allows only
    /// one per member, but the model must not depend on that).
    /// </param>
    /// <param name="reportLocation">Where a diagnostic about this member is reported.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>
    /// The model -- carrying either the emission model or a member-level diagnostic -- or
    /// <see langword="null"/> when the member is not one this generator has anything to say about.
    /// </returns>
    public static WeightedMemberModel? GetModel(
        ISymbol member,
        ImmutableArray<AttributeData> attributes,
        Location? reportLocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        var location = LocationInfo.CreateFrom(reportLocation);
        var typeFqn = SymbolFacts.ToFqn(declaringType);
        var typeDisplayName = declaringType.ToDisplayString();
        var memberDisplayName = typeDisplayName + "." + member.Name;

        var diagnostic = Validate(declaringType, member, typeDisplayName, memberDisplayName, location, out var weightKind);
        if (diagnostic is not null)
        {
            return new WeightedMemberModel(typeFqn, typeDisplayName, member.Name, location, Type: null, diagnostic);
        }

        var typeModel = BuildTypeModel(attributes, declaringType, member, typeFqn, weightKind);
        return new WeightedMemberModel(typeFqn, typeDisplayName, member.Name, location, typeModel, Diagnostic: null);
    }

    /// <summary>
    /// Builds the model for a member this generator rejects before the rules in
    /// <see cref="Validate"/> can even be asked -- currently only SSALR007, where the attribute
    /// landed on a symbol that has no writable name at all.
    /// </summary>
    /// <remarks>
    /// The member still travels through the pipeline as a <see cref="WeightedMemberModel"/> rather
    /// than being reported on the spot, because it counts towards the "one weight member per type"
    /// rule (SSALR002) like any other decorated member. <paramref name="memberName"/> is therefore
    /// the name as it appears in source, never a compiler-internal one: it is what SSALR002's member
    /// list shows the user.
    /// </remarks>
    /// <param name="declaringType">The type the rejected member belongs to.</param>
    /// <param name="memberName">The member's source-level name.</param>
    /// <param name="reportLocation">Where the diagnostic is reported.</param>
    /// <param name="descriptor">
    /// The rule that fired. It must take the member's display name as <c>{0}</c> and a reason clause
    /// as <c>{1}</c>, which is the shape the member-level rules share.
    /// </param>
    /// <param name="reason">The clause spliced in as <c>{1}</c>.</param>
    /// <returns>A model carrying nothing but the diagnostic.</returns>
    public static WeightedMemberModel CreateRejectedMemberModel(
        INamedTypeSymbol declaringType,
        string memberName,
        Location? reportLocation,
        DiagnosticDescriptor descriptor,
        string reason)
    {
        var location = LocationInfo.CreateFrom(reportLocation);
        var typeDisplayName = declaringType.ToDisplayString();

        return new WeightedMemberModel(
            SymbolFacts.ToFqn(declaringType),
            typeDisplayName,
            memberName,
            location,
            Type: null,
            new DiagnosticInfo(descriptor, location, typeDisplayName + "." + memberName, reason));
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
        if (SymbolFacts.IsGenericOrNestedInGeneric(declaringType))
        {
            return new DiagnosticInfo(DiagnosticDescriptors.GenericTypeNotSupported, location, memberDisplayName);
        }

        // SSALR006: a ref struct cannot be a generic type argument of IReadOnlyList<T>.
        if (declaringType.IsRefLikeType)
        {
            return new DiagnosticInfo(DiagnosticDescriptors.RefStructNotSupported, location, memberDisplayName);
        }

        // SSALR004: the declaring type has to be nameable from a sibling top-level class.
        if (!SymbolFacts.IsAccessibleFromGeneratedCode(declaringType))
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
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol declaringType,
        ISymbol member,
        string typeFqn,
        WeightKind weightKind)
    {
        var namespaceName = SymbolFacts.GetContainingNamespaceName(declaringType);

        var isPublic = SymbolFacts.IsEffectivelyPublic(declaringType)
            && !IsNamedArgumentTrue(attributes, InternalExtensionsArgumentName);

        return new WeightedTypeModel(
            namespaceName,
            typeFqn,
            BuildExtensionClassName(declaringType),
            CSharpNaming.EscapeKeyword(member.Name),
            weightKind,
            isPublic,
            IsNamedArgumentTrue(attributes, SharedSourceOverloadsArgumentName),
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
        return CSharpNaming.JoinIdentifierSegments(names) + ExtensionClassSuffix;
    }

    // HintNameSanitizer strips the "global::" qualifier itself, which is what keeps it out of every
    // generated file name; the qualifier carries no information here.
    private static string BuildHintName(string typeFqn) =>
        HintNameSanitizer.Sanitize(typeFqn + HintNameSuffix);

    /// <summary>
    /// Reads one of the attribute's <see langword="bool"/> named arguments. An argument that was not
    /// written, or was written as <see langword="false"/>, reads as <see langword="false"/> -- which
    /// is also the property's default, so the two are indistinguishable by design.
    /// </summary>
    private static bool IsNamedArgumentTrue(ImmutableArray<AttributeData> attributes, string argumentName)
    {
        foreach (var attribute in attributes)
        {
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == argumentName && namedArgument.Value.Value is true)
                {
                    return true;
                }
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
