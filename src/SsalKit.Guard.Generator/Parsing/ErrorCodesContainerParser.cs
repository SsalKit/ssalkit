using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Guard.Generator.Diagnostics;
using SsalKit.Guard.Generator.Models;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// Turns one <c>[ErrorCodes&lt;TCode&gt;]</c>-decorated class into the candidates the assembler
/// fills with registrations: one per attribute application, since a class may be the container for
/// more than one code enum, and each of those only collects the
/// <c>[ExternalErrorCode&lt;TCode&gt;]</c> registrations written for its own enum.
/// </summary>
internal static class ErrorCodesContainerParser
{
    private const string HintNameSuffix = ".ErrorCodes";

    public static EquatableArray<ErrorCodesContainerCandidate> GetCandidates(
        GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol container)
        {
            return EquatableArray<ErrorCodesContainerCandidate>.Empty;
        }

        var compilation = context.SemanticModel.Compilation;
        var exceptionBase = compilation.GetTypeByMetadataName("System.Exception");
        var externalAttribute = compilation.GetTypeByMetadataName(
            ErrorCodesGenerator.ExternalErrorCodeAttributeMetadataName);

        var containerDisplayName = container.ToDisplayString();
        var shapeDiagnosticFactory = GetShapeDiagnosticFactory(container, containerDisplayName);

        var candidates = ImmutableArray.CreateBuilder<ErrorCodesContainerCandidate>();

        foreach (var attribute in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var codeEnum = attribute.AttributeClass?.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;

            if (codeEnum is null)
            {
                continue;
            }

            var location = LocationInfo.CreateFrom(attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());

            candidates.Add(GetCandidate(
                container,
                containerDisplayName,
                codeEnum,
                exceptionBase,
                externalAttribute,
                location,
                shapeDiagnosticFactory));
        }

        return EquatableArray.Create(candidates.ToImmutable());
    }

    private static ErrorCodesContainerCandidate GetCandidate(
        INamedTypeSymbol container,
        string containerDisplayName,
        INamedTypeSymbol codeEnum,
        INamedTypeSymbol? exceptionBase,
        INamedTypeSymbol? externalAttribute,
        LocationInfo? location,
        System.Func<LocationInfo?, DiagnosticInfo>? shapeDiagnosticFactory)
    {
        var codeEnumFqn = SymbolFacts.ToFqn(codeEnum);

        // A container the generator refuses to fill still counts as declared: its code enum stays
        // out of SSALG008, so the user sees the one rule they have to fix and not one warning per
        // exception on top of it.
        if (shapeDiagnosticFactory is not null)
        {
            return new ErrorCodesContainerCandidate(
                IsValid: false,
                TCodeFqn: codeEnumFqn,
                TCodeDisplayName: codeEnum.ToDisplayString(),
                TCodeIsEffectivelyPublic: false,
                ContainerFqn: SymbolFacts.ToFqn(container),
                ContainerDisplayName: containerDisplayName,
                Namespace: string.Empty,
                ContainingTypeDeclarations: EquatableArray<string>.Empty,
                ContainerDeclaration: string.Empty,
                HintName: string.Empty,
                ExternalRegistrations: EquatableArray<ExternalRegistrationCandidate>.Empty,
                Location: location,
                Diagnostic: shapeDiagnosticFactory(location));
        }

        var containingNamespace = container.ContainingNamespace;
        var namespaceName = containingNamespace is null || containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString();

        return new ErrorCodesContainerCandidate(
            IsValid: true,
            TCodeFqn: codeEnumFqn,
            TCodeDisplayName: codeEnum.ToDisplayString(),
            TCodeIsEffectivelyPublic: SymbolFacts.IsEffectivelyPublic(codeEnum),
            ContainerFqn: SymbolFacts.ToFqn(container),
            ContainerDisplayName: containerDisplayName,
            Namespace: namespaceName,
            ContainingTypeDeclarations: GetContainingTypeDeclarations(container),
            ContainerDeclaration: GuardSymbolFacts.ToPartialDeclaration(container),
            HintName: BuildHintName(container, codeEnum),
            ExternalRegistrations: GetExternalRegistrations(
                container, containerDisplayName, codeEnum, exceptionBase, externalAttribute),
            Location: location,
            Diagnostic: null);
    }

    /// <summary>
    /// Returns a factory for the diagnostic that disqualifies the container as a whole, or
    /// <see langword="null"/> when its shape is fine. A factory rather than a diagnostic because
    /// each <c>[ErrorCodes]</c> application on the class reports at its own location.
    /// </summary>
    private static System.Func<LocationInfo?, DiagnosticInfo>? GetShapeDiagnosticFactory(
        INamedTypeSymbol container, string containerDisplayName)
    {
        // SSALG007 first: a generic container is rejected outright, so telling the user to also add
        // 'partial' would be pointing at the smaller of two problems.
        if (SymbolFacts.IsGenericOrNestedInGeneric(container))
        {
            return location => new DiagnosticInfo(
                DiagnosticDescriptors.ContainerCannotBeGeneric, location, containerDisplayName);
        }

        var reason = GetWrongShapeReason(container);

        return reason is null
            ? null
            : location => new DiagnosticInfo(
                DiagnosticDescriptors.ContainerMustBeStaticPartialClass, location, containerDisplayName, reason);
    }

    /// <summary>
    /// Returns the clause naming every way the container is not a <c>static partial class</c>, or
    /// <see langword="null"/> when it is one.
    /// </summary>
    private static string? GetWrongShapeReason(INamedTypeSymbol container)
    {
        var reasons = new List<string>();

        // A record is a class too, but its generated part would have to be re-declared as a record;
        // reporting it as "not a class" is close enough to point at the fix, which is to move the
        // container onto a plain static class.
        if (container.TypeKind != TypeKind.Class || container.IsRecord)
        {
            reasons.Add("not a class");
        }

        if (!container.IsStatic)
        {
            reasons.Add("not static");
        }

        if (!GuardSymbolFacts.IsPartial(container))
        {
            reasons.Add("not partial");
        }

        return reasons.Count == 0 ? null : string.Join(" and ", reasons);
    }

    private static EquatableArray<string> GetContainingTypeDeclarations(INamedTypeSymbol container)
    {
        var declarations = new List<string>();
        for (var current = container.ContainingType; current is not null; current = current.ContainingType)
        {
            declarations.Add(GuardSymbolFacts.ToPartialDeclaration(current));
        }

        declarations.Reverse();
        return EquatableArray.Create(declarations.ToImmutableArray());
    }

    /// <summary>
    /// Reads the container's <c>[ExternalErrorCode&lt;TCode&gt;]</c> attributes for one code enum,
    /// off the symbol rather than through a provider of their own: they are declared on the
    /// container, and a separate provider would only have to be joined back to it.
    /// </summary>
    private static EquatableArray<ExternalRegistrationCandidate> GetExternalRegistrations(
        INamedTypeSymbol container,
        string containerDisplayName,
        INamedTypeSymbol codeEnum,
        INamedTypeSymbol? exceptionBase,
        INamedTypeSymbol? externalAttribute)
    {
        var registrations = ImmutableArray.CreateBuilder<ExternalRegistrationCandidate>();

        foreach (var attribute in container.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null
                || !SymbolEqualityComparer.Default.Equals(attributeClass.OriginalDefinition, externalAttribute)
                || attributeClass.TypeArguments.Length != 1
                || !SymbolEqualityComparer.Default.Equals(attributeClass.TypeArguments[0], codeEnum)
                || attribute.ConstructorArguments.Length != 2)
            {
                continue;
            }

            var location = LocationInfo.CreateFrom(attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
            var registered = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (registered is null)
            {
                // 'typeof(...)' of something that is not a named type, or an unresolved type: the
                // compiler already reports the malformed argument.
                continue;
            }

            registrations.Add(GetExternalRegistration(
                registered, containerDisplayName, codeEnum, exceptionBase, attribute, location));
        }

        return EquatableArray.Create(registrations.ToImmutable());
    }

    private static ExternalRegistrationCandidate GetExternalRegistration(
        INamedTypeSymbol registered,
        string containerDisplayName,
        INamedTypeSymbol codeEnum,
        INamedTypeSymbol? exceptionBase,
        AttributeData attribute,
        LocationInfo? location)
    {
        var registeredDisplayName = registered.ToDisplayString();
        var depth = GuardSymbolFacts.GetExceptionDepth(registered, exceptionBase);

        var reason = registered.IsUnboundGenericType
            ? "it is an unbound generic type"
            : depth is null
                ? "it does not derive from 'System.Exception'"
                : null;

        if (reason is not null)
        {
            return new ExternalRegistrationCandidate(
                IsValid: false,
                ExceptionFqn: string.Empty,
                ExceptionDisplayName: registeredDisplayName,
                CodeExpression: string.Empty,
                InheritanceDepth: 0,
                Location: location,
                Diagnostic: new DiagnosticInfo(
                    DiagnosticDescriptors.ExternalTypeMustBeAnException,
                    location,
                    registeredDisplayName,
                    containerDisplayName,
                    reason));
        }

        return new ExternalRegistrationCandidate(
            IsValid: true,
            ExceptionFqn: SymbolFacts.ToFqn(registered),
            ExceptionDisplayName: registeredDisplayName,
            CodeExpression: GuardSymbolFacts.ToCodeExpression(attribute.ConstructorArguments[1], codeEnum),
            InheritanceDepth: depth!.Value,
            Location: location,
            Diagnostic: null);
    }

    /// <summary>
    /// The hint name carries the code enum as well as the container, because one class may be the
    /// container for several enums and every generated file needs its own name.
    /// </summary>
    /// <remarks>
    /// Both names arrive <c>global::</c>-qualified and neither is pre-stripped here:
    /// <c>HintNameSanitizer</c> removes the alias qualifier wherever it appears, including the one
    /// in the middle of this pair.
    /// </remarks>
    private static string BuildHintName(INamedTypeSymbol container, INamedTypeSymbol codeEnum) =>
        HintNameSanitizer.Sanitize(
            SymbolFacts.ToFqn(container) + "." + SymbolFacts.ToFqn(codeEnum) + HintNameSuffix);
}
