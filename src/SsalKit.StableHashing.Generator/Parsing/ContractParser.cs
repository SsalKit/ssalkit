using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.StableHashing.Generator.Diagnostics;
using SsalKit.StableHashing.Generator.Models;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Turns one <c>[StableHashContract]</c>-decorated type into a <see cref="ContractModel"/>: every
/// type-level and member-level diagnostic it produces, and, when nothing blocking was found, the
/// members ready to emit.
/// </summary>
/// <remarks>
/// Unlike a per-member <c>ForAttributeWithMetadataName</c> pipeline (the shape
/// <c>SsalKit.Randomness.Generator</c> uses), this runs once per contract *type*, driven by
/// <c>[StableHashContract]</c> rather than by each member's own attribute. That works because
/// <see cref="ITypeSymbol.GetMembers()"/> already returns a type's full, partial-declarations-merged
/// member list in one call, so there is no need for Randomness's separate collect-then-group stage
/// just to see every member of a type together -- one transform invocation already has all of them.
/// </remarks>
internal static class ContractParser
{
    public static ContractModel Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var type = (INamedTypeSymbol)context.TargetSymbol;
        var contractAttribute = context.Attributes[0];

        var typeFqn = SymbolFacts.ToFqn(type);
        var typeDisplayName = type.ToDisplayString();
        var namespaceName = SymbolFacts.GetContainingNamespaceName(type);
        var extensionClassName = ContractNaming.BuildExtensionClassName(type);
        var hintName = ContractNaming.BuildHintName(typeFqn);
        var isClassContract = type.TypeKind == TypeKind.Class;
        // Effectively public only when the type and every type containing it are public; anything
        // else (internal, protected internal, or a nesting chain that dips below either) downgrades
        // the generated extension class to internal rather than being rejected -- SSALH007 already
        // owns the "truly inaccessible" case (private/protected/file-local).
        var isPublic = SymbolFacts.IsEffectivelyPublic(type);
        var contractLocation = AttributeLocations.GetLocationInfo(contractAttribute, type);

        var (name, version) = ReadNameAndVersion(contractAttribute);
        var nameIsValid = !string.IsNullOrWhiteSpace(name) && version >= 1;

        var diagnostics = new List<DiagnosticInfo>();

        var isGeneric = SymbolFacts.IsGenericOrNestedInGeneric(type);
        if (isGeneric)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.GenericContractNotSupported, contractLocation, typeDisplayName));
        }

        if (!nameIsValid)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidContractNameOrVersion, contractLocation, typeDisplayName, DescribeNameOrVersionProblem(name, version)));
        }

        if (isClassContract && (!type.IsSealed || type.IsStatic))
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.ClassContractNotSealed, contractLocation, typeDisplayName));
        }

        var accessibleFromGeneratedCode = SymbolFacts.IsAccessibleFromGeneratedCode(type);
        if (!accessibleFromGeneratedCode)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.MemberNotAccessibleToGeneratedCode,
                contractLocation,
                typeDisplayName,
                "the contract type itself, or a type it is nested inside,"));
        }

        var members = EquatableArray<MemberModel>.Empty;

        // Member analysis is skipped for a generic or unreachable type: its members' emitted forms
        // would either not compile (an open type parameter has no closed shape to write) or could
        // never be called from generated code anyway, so nothing further is learned from them.
        if (!isGeneric && accessibleFromGeneratedCode)
        {
            if (CycleDetector.HasCycle(type))
            {
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.CircularContractGraph, contractLocation, typeDisplayName));
            }

            members = ParseMembers(type, typeDisplayName, diagnostics);
        }

        var hasErrors = diagnostics.Any(diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);

        return new ContractModel(
            typeFqn,
            typeDisplayName,
            namespaceName,
            extensionClassName,
            hintName,
            isClassContract,
            isPublic,
            nameIsValid ? name : null,
            version,
            contractLocation,
            ReadyToEmit: !hasErrors,
            members,
            diagnostics.ToEquatableArray());
    }

    private static (string? Name, int Version) ReadNameAndVersion(AttributeData contractAttribute)
    {
        string? name = contractAttribute.ConstructorArguments.Length == 1
            ? contractAttribute.ConstructorArguments[0].Value as string
            : null;

        var version = 1;
        foreach (var namedArgument in contractAttribute.NamedArguments)
        {
            if (namedArgument.Key == "Version" && namedArgument.Value.Value is int explicitVersion)
            {
                version = explicitVersion;
            }
        }

        return (name, version);
    }

    private static string DescribeNameOrVersionProblem(string? name, int version)
    {
        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            reasons.Add("its name is null or consists only of whitespace");
        }

        if (version < 1)
        {
            reasons.Add("its Version (" + version.ToString(CultureInfo.InvariantCulture) + ") is less than 1");
        }

        return string.Join(" and ", reasons);
    }

    private static EquatableArray<MemberModel> ParseMembers(
        INamedTypeSymbol type, string typeDisplayName, List<DiagnosticInfo> diagnostics)
    {
        var candidates = type.GetMembers()
            .Where(member => member is IFieldSymbol or IPropertySymbol && ContractAttributeInfo.HasMemberAttribute(member))
            .ToList();

        if (candidates.Count == 0)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.ContractHasNoMembers, null, typeDisplayName));
            return EquatableArray<MemberModel>.Empty;
        }

        // Phase 1: accessibility. A member that cannot be read has nothing else worth checking.
        var accessible = new List<ISymbol>();
        foreach (var member in candidates)
        {
            var memberAttribute = ContractAttributeInfo.FindMemberAttribute(member)!;
            var location = AttributeLocations.GetLocationInfo(memberAttribute, member);
            var memberDisplayName = typeDisplayName + "." + member.Name;

            var problem = GetMemberAccessProblem(member);
            if (problem is not null)
            {
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MemberNotAccessibleToGeneratedCode, location, memberDisplayName, problem));
                continue;
            }

            accessible.Add(member);
        }

        // Phase 2: id range.
        var idCandidates = new List<(int Id, ISymbol Member, LocationInfo? Location, string DisplayName)>();
        foreach (var member in accessible)
        {
            var memberAttribute = ContractAttributeInfo.FindMemberAttribute(member)!;
            var location = AttributeLocations.GetLocationInfo(memberAttribute, member);
            var memberDisplayName = typeDisplayName + "." + member.Name;

            var id = GetMemberId(memberAttribute);
            if (id is null)
            {
                // The attribute's sole constructor argument could not be read as an int -- only
                // reachable when the application itself is malformed, which the compiler already
                // reports on its own terms. Nothing useful to add here.
                continue;
            }

            if (id.Value < 1)
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.MemberIdOutOfRange, location, memberDisplayName, id.Value.ToString(CultureInfo.InvariantCulture)));
                continue;
            }

            idCandidates.Add((id.Value, member, location, memberDisplayName));
        }

        // Phase 3: id uniqueness within this contract.
        var typeReady = new List<(int Id, ISymbol Member, LocationInfo? Location, string DisplayName)>();
        foreach (var group in idCandidates.GroupBy(candidate => candidate.Id))
        {
            var groupList = group.ToList();
            if (groupList.Count == 1)
            {
                typeReady.Add(groupList[0]);
                continue;
            }

            var memberList = string.Join(
                ", ", groupList.Select(candidate => "'" + candidate.Member.Name + "'").OrderBy(name => name, System.StringComparer.Ordinal));

            foreach (var candidate in groupList)
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.DuplicateMemberId,
                    candidate.Location,
                    typeDisplayName,
                    candidate.Id.ToString(CultureInfo.InvariantCulture),
                    memberList));
            }
        }

        // Phase 4: type classification.
        var models = new List<MemberModel>();
        foreach (var candidate in typeReady)
        {
            var memberType = candidate.Member is IFieldSymbol field ? field.Type : ((IPropertySymbol)candidate.Member).Type;
            var classification = TypeClassifier.Classify(memberType);

            if (classification.IsError)
            {
                var args = new string[classification.ErrorArgs.Length + 1];
                args[0] = candidate.DisplayName;
                classification.ErrorArgs.CopyTo(args, 1);

                diagnostics.Add(new DiagnosticInfo(classification.ErrorDescriptor!, candidate.Location, args));
                continue;
            }

            var accessExpression = "value." + CSharpNaming.EscapeKeyword(candidate.Member.Name);
            models.Add(new MemberModel(candidate.Id, accessExpression, classification.Shape!));
        }

        return models.ToEquatableArray();
    }

    private static string? GetMemberAccessProblem(ISymbol member)
    {
        if (member.IsStatic)
        {
            return "it is static";
        }

        if (member is IPropertySymbol { IsIndexer: true })
        {
            return "it is an indexer";
        }

        if (member is IPropertySymbol { GetMethod: null })
        {
            return "it is a write-only property";
        }

        if (!SymbolFacts.IsAtLeastInternal(member.DeclaredAccessibility))
        {
            return "it is not accessible to the generated extension class";
        }

        if (member is IPropertySymbol { GetMethod: { } getter } && !SymbolFacts.IsAtLeastInternal(getter.DeclaredAccessibility))
        {
            return "its getter is not accessible to the generated extension class";
        }

        return null;
    }

    private static int? GetMemberId(AttributeData memberAttribute) =>
        memberAttribute.ConstructorArguments.Length == 1 ? memberAttribute.ConstructorArguments[0].Value as int? : null;
}
