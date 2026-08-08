using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SsalKit.Generators.Toolkit;
using SsalKit.Randomness.Generator.Diagnostics;
using SsalKit.Randomness.Generator.Models;

namespace SsalKit.Randomness.Generator.Parsing;

/// <summary>
/// Handles the <c>[RandomWeight]</c> applications that <c>ForAttributeWithMetadataName</c> structurally
/// cannot see: the ones written with an attribute target specifier that moves the attribute onto a
/// symbol other than the one the decorated syntax node declares.
/// </summary>
/// <remarks>
/// <para>
/// <c>ForAttributeWithMetadataName</c> matches an attribute against the symbol declared by the node
/// the attribute list sits on. A target specifier breaks that correspondence: <c>[property:
/// RandomWeight]</c> on a positional record parameter attaches to the record's synthesized property,
/// and <c>[field: RandomWeight]</c> attaches to an auto-property's backing field -- neither of which
/// is the parameter's or the property's own symbol. Those applications are therefore never reported
/// by the attribute provider, and without this second branch they would be silently ignored: no
/// generated code, no diagnostic, nothing to tell the user their attribute did nothing.
/// </para>
/// <para>
/// The branch deliberately claims only the three combinations below. Everything else is somebody
/// else's job, and claiming more would mean reporting the same application twice:
/// </para>
/// <list type="table">
///   <listheader><term>Declaration</term><description>Target and handling</description></listheader>
///   <item>
///     <term>Positional record parameter</term>
///     <description>
///     <c>property:</c> -- promoted to the synthesized property and run through the ordinary rules;
///     <c>field:</c> -- SSALR007.
///     </description>
///   </item>
///   <item>
///     <term>Property declaration</term>
///     <description>
///     <c>field:</c> -- SSALR007. Its <c>property:</c> form lands on the property symbol itself,
///     which the attribute provider already reports, so taking it here too would model the one
///     application twice and trip SSALR002 ("more than one weight member") on a type that declares
///     one.
///     </description>
///   </item>
/// </list>
/// <para>
/// Applications the compiler itself rejects are left alone: an untargeted <c>[RandomWeight]</c> on a
/// positional parameter is a CS0592 error (the attribute's usage does not allow a parameter), and a
/// <c>property:</c> target that has no synthesized property to attach to -- shadowed by a
/// user-declared member, or on a non-record primary constructor -- is a CS0657 warning after which
/// the attribute exists on no symbol at all. Both are found by looking for the symbol that actually
/// carries the application and finding none, so no special case is needed for either.
/// </para>
/// </remarks>
internal static class TargetRedirectedRandomWeightParser
{
    /// <summary>The <c>global::</c>-qualified name the attribute's resolved type must have.</summary>
    private const string RandomWeightAttributeFqn =
        "global::" + RandomWeightGenerator.RandomWeightAttributeMetadataName;

    private const string AttributeShortName = "RandomWeight";
    private const string AttributeTypeName = "RandomWeightAttribute";
    private const string PropertyTarget = "property";
    private const string FieldTarget = "field";

    /// <summary>
    /// The syntax-only half of the branch: whether <paramref name="node"/> is an attribute
    /// application whose shape this branch could possibly claim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs for every node of every changed syntax tree, so it reads syntax only -- node kinds,
    /// the target keyword, and the attribute's written name -- and never touches the semantic model.
    /// Whether the name really resolves to our attribute is settled in
    /// <see cref="GetModel(GeneratorSyntaxContext, CancellationToken)"/>, on the far smaller set of
    /// nodes that get this far.
    /// </para>
    /// <para>
    /// Matching the written name means an attribute reached through a <c>using</c> alias
    /// (<c>using RW = SsalKit.Randomness.RandomWeightAttribute;</c>) is not recognised, and such an
    /// application falls back to being ignored. <c>ForAttributeWithMetadataName</c> does resolve
    /// aliases, so the two branches are asymmetric there; an alias combined with a redirected target
    /// is rare enough that paying for an alias index over every attribute in the compilation is the
    /// worse trade.
    /// </para>
    /// </remarks>
    /// <param name="node">The syntax node offered by the pipeline.</param>
    /// <returns><see langword="true"/> when the node is worth a semantic look.</returns>
    public static bool IsCandidate(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute
            || attribute.Parent is not AttributeListSyntax attributeList
            || attributeList.Target is null
            || !IsRandomWeightName(attribute.Name))
        {
            return false;
        }

        var target = attributeList.Target.Identifier.ValueText;

        return attributeList.Parent switch
        {
            ParameterSyntax => target is PropertyTarget or FieldTarget,
            PropertyDeclarationSyntax => target is FieldTarget,
            _ => false,
        };
    }

    /// <summary>
    /// The semantic half: resolves the attribute, finds the symbol the compiler actually put it on,
    /// and produces either a promoted member model or SSALR007.
    /// </summary>
    /// <param name="context">The pipeline's syntax context, positioned on the attribute application.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>
    /// The member model, or <see langword="null"/> when the application is not ours, is one the
    /// compiler has already rejected, or is otherwise nothing to report.
    /// </returns>
    public static WeightedMemberModel? GetModel(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Node is not AttributeSyntax attribute
            || attribute.Parent is not AttributeListSyntax attributeList
            || attributeList.Target is null
            || attributeList.Parent is not { } declaration)
        {
            return null;
        }

        // The predicate matched the name as written; this is what rules out an unrelated attribute
        // that happens to be spelled the same.
        if (!IsRandomWeightAttribute(context.SemanticModel, attribute, cancellationToken))
        {
            return null;
        }

        var declaringType = GetDeclaringType(context.SemanticModel, declaration, cancellationToken);
        if (declaringType is null)
        {
            return null;
        }

        // Which symbol carries the application is decided by the application's own syntax, not by
        // matching names: the redirected symbol may be named anything (a backing field is not even
        // named after its property in a way that could be matched), and "no member carries it" is
        // precisely the signal that the compiler discarded the attribute.
        var carrier = FindCarrier(declaringType, attribute);
        if (carrier is null)
        {
            return null;
        }

        var (member, attributeData) = carrier.Value;

        return attributeList.Target.Identifier.ValueText == FieldTarget
            ? GetBackingFieldModel(declaringType, member, declaration, attribute)
            : GetPromotedPropertyModel(member, attributeData, attribute, cancellationToken);
    }

    /// <summary>
    /// Reports SSALR007 for an attribute that landed on a compiler-generated backing field.
    /// </summary>
    /// <remarks>
    /// The member is only reported when the carrier really is a field standing behind another member:
    /// a property implemented over some other state has no backing field for the attribute to attach
    /// to, and the compiler discards a <c>field:</c> attribute written on one (CS0657) rather than
    /// putting it somewhere else. The name in the diagnostic is the associated member's -- the one the
    /// user wrote -- and never the backing field's internal name, which would be both unhelpful here
    /// and wrong in SSALR002's member list.
    /// </remarks>
    private static WeightedMemberModel? GetBackingFieldModel(
        INamedTypeSymbol declaringType, ISymbol member, SyntaxNode declaration, AttributeSyntax attribute) =>
        member is IFieldSymbol { AssociatedSymbol: { } associatedMember }
            ? RandomWeightMemberParser.CreateRejectedMemberModel(
                declaringType,
                associatedMember.Name,
                attribute.GetLocation(),
                DiagnosticDescriptors.BackingFieldNotSupported,
                GetBackingFieldFix(declaration))
            : null;

    /// <summary>
    /// Runs the ordinary member rules against the property an attribute was redirected onto.
    /// </summary>
    /// <remarks>
    /// A record's synthesized property is an ordinary readable instance property, so nothing about it
    /// needs special handling: reusing the shared parser is what makes an unsupported weight type
    /// (SSALR001), a generic record (SSALR005), and every other rule fire here exactly as they do for
    /// a hand-written property, and what makes the emitted extensions identical to the ones a
    /// hand-written property would have produced.
    /// </remarks>
    private static WeightedMemberModel? GetPromotedPropertyModel(
        ISymbol member, AttributeData attributeData, AttributeSyntax attribute, CancellationToken cancellationToken) =>
        member is IPropertySymbol property
            ? RandomWeightMemberParser.GetModel(
                property, ImmutableArray.Create(attributeData), attribute.GetLocation(), cancellationToken)
            : null;

    /// <summary>
    /// Names the nameable weight member the user should have decorated instead, as SSALR007's reason
    /// clause.
    /// </summary>
    private static string GetBackingFieldFix(SyntaxNode declaration) =>
        declaration is ParameterSyntax
            ? "write '[property: RandomWeight]' instead, which puts the attribute on the property the record synthesizes for this parameter"
            : "apply '[RandomWeight]' to the property itself, with no target specifier";

    /// <summary>
    /// Returns the member of <paramref name="declaringType"/> whose attribute list holds the
    /// application written at <paramref name="attribute"/>, together with that application.
    /// </summary>
    /// <remarks>
    /// Both redirect targets land on a member of the type the decorated declaration belongs to -- a
    /// synthesized property or a backing field -- so the type's own members are the whole search
    /// space. A <see langword="null"/> result means no symbol carries the attribute, which is the
    /// case for every form the compiler discards.
    /// </remarks>
    private static (ISymbol Member, AttributeData Data)? FindCarrier(
        INamedTypeSymbol declaringType, AttributeSyntax attribute)
    {
        foreach (var member in declaringType.GetMembers())
        {
            foreach (var attributeData in member.GetAttributes())
            {
                if (IsApplicationOf(attributeData, attribute))
                {
                    return (member, attributeData);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="attributeData"/> is the application written at
    /// <paramref name="attribute"/>, compared by syntax tree and span.
    /// </summary>
    /// <remarks>
    /// The span is compared rather than the materialized node, so nothing here re-parses a lazily
    /// read tree just to identify the application it already points at.
    /// </remarks>
    private static bool IsApplicationOf(AttributeData attributeData, AttributeSyntax attribute)
    {
        var reference = attributeData.ApplicationSyntaxReference;

        return reference is not null
            && reference.SyntaxTree == attribute.SyntaxTree
            && reference.Span == attribute.Span;
    }

    /// <summary>
    /// The type that declares the member an attribute was redirected onto.
    /// </summary>
    private static INamedTypeSymbol? GetDeclaringType(
        SemanticModel semanticModel, SyntaxNode declaration, CancellationToken cancellationToken) =>
        declaration switch
        {
            ParameterSyntax parameter => semanticModel.GetDeclaredSymbol(parameter, cancellationToken)?.ContainingType,
            PropertyDeclarationSyntax property => semanticModel.GetDeclaredSymbol(property, cancellationToken)?.ContainingType,
            _ => null,
        };

    /// <summary>
    /// Whether the written attribute name resolves to <c>SsalKit.Randomness.RandomWeightAttribute</c>.
    /// </summary>
    /// <remarks>
    /// An attribute application's own symbol is the constructor it binds to, so the type is one hop
    /// up. When the constructor does not bind at all -- a wrong argument list, say -- the application
    /// is already a compiler error, and staying silent leaves the user with that one error rather
    /// than a second one about an attribute they are in the middle of writing.
    /// </remarks>
    private static bool IsRandomWeightAttribute(
        SemanticModel semanticModel, AttributeSyntax attribute, CancellationToken cancellationToken)
    {
        var constructor = semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol as IMethodSymbol;

        return constructor is not null
            && SymbolFacts.ToFqn(constructor.ContainingType) == RandomWeightAttributeFqn;
    }

    /// <summary>
    /// Whether an attribute name is written as <c>RandomWeight</c> or <c>RandomWeightAttribute</c>,
    /// looking only at the rightmost segment so a qualified or alias-qualified spelling matches too.
    /// </summary>
    private static bool IsRandomWeightName(NameSyntax name)
    {
        var rightmost = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };

        return rightmost?.Identifier.ValueText is AttributeShortName or AttributeTypeName;
    }
}
