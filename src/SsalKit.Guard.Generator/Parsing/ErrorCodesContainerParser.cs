using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Guard.Generator.Diagnostics;
using SsalKit.Guard.Generator.Models;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// Turns one <c>[ErrorCodes&lt;TCode&gt;]</c>-decorated class into the candidate the assembler fills
/// with registrations, together with the <c>[ExternalErrorCode&lt;TCode&gt;]</c> registrations
/// written on it.
/// </summary>
/// <remarks>
/// Exactly one candidate per class, because a class can be the container for exactly one code enum:
/// <c>[AttributeUsage(AllowMultiple = false)]</c> is enforced against the attribute's generic
/// definition, so <c>[ErrorCodes&lt;A&gt;][ErrorCodes&lt;B&gt;]</c> is CS0579 at the declaration
/// site and never reaches the generator.
/// </remarks>
internal static class ErrorCodesContainerParser
{
    private const string HintNameSuffix = ".ErrorCodes";

    public static ErrorCodesContainerCandidate? GetCandidate(
        GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol container)
        {
            return null;
        }

        // Only ever one application: see the remarks on this type.
        var attribute = context.Attributes[0];

        var codeEnum = attribute.AttributeClass?.TypeArguments.Length == 1
            ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
            : null;

        if (codeEnum is null)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;
        var location = LocationInfo.CreateFrom(attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        var containerDisplayName = container.ToDisplayString();
        var codeEnumFqn = SymbolFacts.ToFqn(codeEnum);

        var shapeDiagnostic = GetShapeDiagnostic(container, containerDisplayName, codeEnum, location);

        // A container the generator refuses to fill still counts as declared: its code enum stays
        // out of SSALG008, so the user sees the one rule they have to fix and not one warning per
        // exception on top of it.
        if (shapeDiagnostic is not null)
        {
            return new ErrorCodesContainerCandidate(
                IsValid: false,
                TCodeFqn: codeEnumFqn,
                TCodeDisplayName: codeEnum.ToDisplayString(),
                TCodeIsEffectivelyPublic: false,
                TCodeExternalAssemblyName: string.Empty,
                ContainerFqn: SymbolFacts.ToFqn(container),
                ContainerDisplayName: containerDisplayName,
                ContainerName: container.Name,
                Namespace: string.Empty,
                ContainingTypeDeclarations: EquatableArray<string>.Empty,
                ContainerDeclaration: string.Empty,
                HintName: string.Empty,
                ExternalRegistrations: EquatableArray<ExternalRegistrationCandidate>.Empty,
                Location: location,
                Diagnostic: shapeDiagnostic);
        }

        var exceptionBase = compilation.GetTypeByMetadataName("System.Exception");
        var externalAttribute = compilation.GetTypeByMetadataName(
            ErrorCodesGenerator.ExternalErrorCodeAttributeMetadataName);

        return new ErrorCodesContainerCandidate(
            IsValid: true,
            TCodeFqn: codeEnumFqn,
            TCodeDisplayName: codeEnum.ToDisplayString(),
            TCodeIsEffectivelyPublic: SymbolFacts.IsEffectivelyPublic(codeEnum),
            TCodeExternalAssemblyName: GetExternalAssemblyName(codeEnum, compilation),
            ContainerFqn: SymbolFacts.ToFqn(container),
            ContainerDisplayName: containerDisplayName,
            ContainerName: container.Name,
            Namespace: SymbolFacts.GetContainingNamespaceName(container),
            ContainingTypeDeclarations: GetContainingTypeDeclarations(container),
            ContainerDeclaration: GuardSymbolFacts.ToPartialDeclaration(container),
            HintName: BuildHintName(container, codeEnum),
            ExternalRegistrations: GetExternalRegistrations(
                container, containerDisplayName, codeEnum, exceptionBase, externalAttribute),
            Location: location,
            Diagnostic: null);
    }

    /// <summary>
    /// Returns the name of the assembly declaring <paramref name="codeEnum"/> when it is not the one
    /// being compiled, or <see cref="string.Empty"/> when it is. A container whose code enum comes
    /// from elsewhere can never collect that assembly's <c>[ErrorCode]</c> exceptions, which is what
    /// SSALG011 is about.
    /// </summary>
    private static string GetExternalAssemblyName(INamedTypeSymbol codeEnum, Compilation compilation)
    {
        var declaring = codeEnum.ContainingAssembly;

        return declaring is null || SymbolEqualityComparer.Default.Equals(declaring, compilation.Assembly)
            ? string.Empty
            : declaring.Name;
    }

    /// <summary>
    /// Returns the diagnostic that disqualifies the container as a whole, or <see langword="null"/>
    /// when nothing does.
    /// </summary>
    private static DiagnosticInfo? GetShapeDiagnostic(
        INamedTypeSymbol container,
        string containerDisplayName,
        INamedTypeSymbol codeEnum,
        LocationInfo? location)
    {
        // SSALG007 first: a container (or a code enum) that is rejected outright makes telling the
        // user to also add 'partial' the smaller of two problems.
        var genericReason = GetGenericReason(container, codeEnum);
        if (genericReason is not null)
        {
            return new DiagnosticInfo(
                DiagnosticDescriptors.ContainerCannotBeGeneric, location, containerDisplayName, genericReason);
        }

        var reason = GetWrongShapeReason(container);

        return reason is null
            ? null
            : new DiagnosticInfo(
                DiagnosticDescriptors.ContainerMustBeStaticPartialClass, location, containerDisplayName, reason);
    }

    /// <summary>
    /// Returns why the container or its code enum carries type parameters, or <see langword="null"/>
    /// when neither does.
    /// </summary>
    /// <remarks>
    /// The code enum is rejected as well as the container: an enum nested inside a generic type has
    /// a display name like <c>Outer&lt;int&gt;.Code</c>, which the emitter splices into the generated
    /// documentation and into a <c>cref</c>, where the angle brackets would have to be XML-escaped
    /// rather than written as C#. Refusing the shape outright is better than emitting a file whose
    /// documentation comments do not parse, and an enum nested inside a generic type has no
    /// practical use to trade away for it.
    /// </remarks>
    private static string? GetGenericReason(INamedTypeSymbol container, INamedTypeSymbol codeEnum)
    {
        if (SymbolFacts.IsGenericOrNestedInGeneric(container))
        {
            return "it is generic or is nested inside a generic type";
        }

        return SymbolFacts.IsGenericOrNestedInGeneric(codeEnum)
            ? "its code enum '" + codeEnum.ToDisplayString() + "' is nested inside a generic type"
            : null;
    }

    /// <summary>
    /// Returns the clause naming every way the container is not a <c>static partial class</c> a
    /// generated file could attach a second part to, or <see langword="null"/> when it is one.
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

        // A file-local container reports as 'internal' and can be 'static partial', so it passes
        // every check above -- while the generated part, being another file, would declare a
        // second, unrelated type of the same name. Nothing would fail to compile; the user's
        // container would simply stay empty.
        if (container.IsFileLocal)
        {
            reasons.Add("file-local");
        }

        // The generated file re-declares the whole nesting chain, so every type in it has to be
        // partial too. Without this the user gets CS0260 in generated code instead of a rule.
        var nonPartialContainingType = FindNonPartialContainingType(container);
        if (nonPartialContainingType is not null)
        {
            reasons.Add("nested inside '" + nonPartialContainingType.ToDisplayString() + "', which is not partial");
        }

        return reasons.Count == 0 ? null : string.Join(" and ", reasons);
    }

    private static INamedTypeSymbol? FindNonPartialContainingType(INamedTypeSymbol container)
    {
        for (var current = container.ContainingType; current is not null; current = current.ContainingType)
        {
            if (!GuardSymbolFacts.IsPartial(current))
            {
                return current;
            }
        }

        return null;
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
    /// Reads the container's <c>[ExternalErrorCode&lt;TCode&gt;]</c> attributes off the symbol rather
    /// than through a provider of their own: they are declared on the container, and a separate
    /// provider would only have to be joined back to it.
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
                || attribute.ConstructorArguments.Length != 2)
            {
                continue;
            }

            var location = LocationInfo.CreateFrom(attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());

            // SSALG010: the container maps exactly one code enum, so a registration naming a
            // different one has nowhere to go. Dropping it silently is how a typo becomes a
            // mapping row that never appears.
            if (attributeClass.TypeArguments[0] is not INamedTypeSymbol registeredCodeEnum
                || !SymbolEqualityComparer.Default.Equals(registeredCodeEnum, codeEnum))
            {
                registrations.Add(new ExternalRegistrationCandidate(
                    IsValid: false,
                    ExceptionFqn: string.Empty,
                    ExceptionDisplayName: string.Empty,
                    CodeExpression: string.Empty,
                    InheritanceDepth: 0,
                    Location: location,
                    Diagnostic: new DiagnosticInfo(
                        DiagnosticDescriptors.ExternalRegistrationForAnotherCodeEnum,
                        location,
                        attributeClass.TypeArguments[0].ToDisplayString(),
                        containerDisplayName,
                        codeEnum.ToDisplayString())));
                continue;
            }

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
    /// The hint name carries the code enum as well as the container. One class maps one enum, so the
    /// container alone would already be unique; including the enum keeps the generated file
    /// self-describing in the IDE's generated-files list at no cost.
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
