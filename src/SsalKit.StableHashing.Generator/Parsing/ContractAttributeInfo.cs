using System.Linq;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// The one place that knows the metadata identity of <c>[StableHashContract]</c> and
/// <c>[StableHashMember]</c>, and how to find one applied to a symbol -- shared by every stage
/// that needs to ask "does this symbol carry this attribute" without pinning a
/// <c>Compilation</c>-specific <see cref="INamedTypeSymbol"/> reference in a cached model.
/// </summary>
internal static class ContractAttributeInfo
{
    private const string Namespace = "SsalKit.StableHashing";
    private const string ContractAttributeTypeName = "StableHashContractAttribute";
    private const string MemberAttributeTypeName = "StableHashMemberAttribute";

    /// <summary>The metadata name <c>ForAttributeWithMetadataName</c> is registered against for contracts.</summary>
    public const string ContractAttributeMetadataName = Namespace + "." + ContractAttributeTypeName;

    /// <summary>The metadata name <c>ForAttributeWithMetadataName</c> is registered against for members.</summary>
    public const string MemberAttributeMetadataName = Namespace + "." + MemberAttributeTypeName;

    /// <summary>Finds the <c>[StableHashContract]</c> application on <paramref name="symbol"/>, if any.</summary>
    public static AttributeData? FindContractAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsAttribute(attribute, ContractAttributeTypeName));

    /// <summary>Whether <paramref name="symbol"/> carries <c>[StableHashContract]</c>.</summary>
    public static bool HasContractAttribute(ISymbol symbol) => FindContractAttribute(symbol) is not null;

    /// <summary>Finds the <c>[StableHashMember]</c> application on <paramref name="symbol"/>, if any.</summary>
    public static AttributeData? FindMemberAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsAttribute(attribute, MemberAttributeTypeName));

    /// <summary>Whether <paramref name="symbol"/> carries <c>[StableHashMember]</c>.</summary>
    public static bool HasMemberAttribute(ISymbol symbol) => FindMemberAttribute(symbol) is not null;

    private static bool IsAttribute(AttributeData attribute, string typeName) =>
        attribute.AttributeClass is { } attributeClass
        && attributeClass.MetadataName == typeName
        && SymbolFacts.GetContainingNamespaceName(attributeClass) == Namespace;
}
