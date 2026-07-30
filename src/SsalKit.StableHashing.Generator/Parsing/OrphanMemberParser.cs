using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.StableHashing.Generator.Diagnostics;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Reports SSALH012: a member decorated with <c>[StableHashMember]</c> whose declaring type has
/// no <c>[StableHashContract]</c>.
/// </summary>
/// <remarks>
/// This is the one rule <see cref="ContractParser"/> cannot see: it only ever runs for a type that
/// *does* carry <c>[StableHashContract]</c> (it is driven by that attribute), so a member on a
/// type that never got the contract attribute at all is invisible to it. Driving this check off
/// <c>[StableHashMember]</c> directly, independently of <see cref="ContractParser"/>'s pipeline,
/// is what makes the orphan case visible: every application of the member attribute is examined
/// once, and the only question asked is whether its declaring type also carries the contract
/// attribute.
/// </remarks>
internal static class OrphanMemberParser
{
    public static DiagnosticInfo? GetOrphanDiagnostic(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var member = context.TargetSymbol;
        var declaringType = member.ContainingType;

        if (declaringType is null || ContractAttributeInfo.HasContractAttribute(declaringType))
        {
            return null;
        }

        var memberAttribute = context.Attributes[0];
        var location = AttributeLocations.GetLocationInfo(memberAttribute, member);
        var memberDisplayName = declaringType.ToDisplayString() + "." + member.Name;

        return new DiagnosticInfo(DiagnosticDescriptors.OrphanMemberAttribute, location, memberDisplayName, declaringType.ToDisplayString());
    }
}
