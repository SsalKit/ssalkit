using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Guard.Generator.Diagnostics;
using SsalKit.Guard.Generator.Models;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// Turns one <c>[ErrorCode&lt;TCode&gt;]</c>-decorated class into the candidate the assembler joins
/// with the containers.
/// </summary>
/// <remarks>
/// Exactly one candidate per class, because a type can declare exactly one code:
/// <c>[AttributeUsage(AllowMultiple = false)]</c> is enforced against the attribute's generic
/// definition, so <c>[ErrorCode&lt;A&gt;(…)][ErrorCode&lt;B&gt;(…)]</c> is CS0579 at the declaration
/// site and never reaches the generator.
/// </remarks>
internal static class ErrorCodeExceptionParser
{
    public static ErrorCodeExceptionCandidate? GetCandidate(
        GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // [AttributeUsage(Class)] already rejects everything else at the application site.
        if (context.TargetSymbol is not INamedTypeSymbol exception)
        {
            return null;
        }

        // Only ever one application: see the remarks on this type.
        var attribute = context.Attributes[0];

        var codeEnum = attribute.AttributeClass?.TypeArguments.Length == 1
            ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
            : null;

        // The attribute's own 'where TCode : struct, Enum' constraint means anything else is
        // already a compiler error at the application site.
        if (codeEnum is null || attribute.ConstructorArguments.Length != 1)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;

        return GetCandidate(
            attribute,
            exception,
            codeEnum,
            compilation.GetTypeByMetadataName("System.Exception"),
            compilation.GetTypeByMetadataName(GuardSymbolFacts.ErrorCodedExceptionMetadataName));
    }

    private static ErrorCodeExceptionCandidate GetCandidate(
        AttributeData attribute,
        INamedTypeSymbol exception,
        INamedTypeSymbol codeEnum,
        INamedTypeSymbol? exceptionBase,
        INamedTypeSymbol? errorCodedException)
    {
        var location = LocationInfo.CreateFrom(attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        var exceptionDisplayName = exception.ToDisplayString();

        // SSALG001 first: "is this even an error-coded exception" is the more fundamental question,
        // and reporting both rules at once would only ask the user to fix a type they may have to
        // rewrite anyway.
        if (!GuardSymbolFacts.DerivesFrom(exception, errorCodedException))
        {
            return ErrorCodeExceptionCandidate.Invalid(
                location,
                new DiagnosticInfo(
                    DiagnosticDescriptors.ExceptionMustDeriveFromErrorCodedException, location, exceptionDisplayName));
        }

        // SSALG009 next: whether the generated file may name the type at all comes before what it
        // would do with the name. Reporting "abstract" for a type the container could never mention
        // would send the user to fix the wrong half of the declaration.
        var inaccessibleReason = GuardSymbolFacts.GetInaccessibleReason(exception);
        if (inaccessibleReason is not null)
        {
            return ErrorCodeExceptionCandidate.Invalid(
                location,
                new DiagnosticInfo(
                    DiagnosticDescriptors.ExceptionMustBeAccessible,
                    location,
                    exceptionDisplayName,
                    inaccessibleReason));
        }

        var unusableReason = GetUnusableReason(exception);
        if (unusableReason is not null)
        {
            return ErrorCodeExceptionCandidate.Invalid(
                location,
                new DiagnosticInfo(
                    DiagnosticDescriptors.ExceptionMustBeConcreteAndNonGeneric,
                    location,
                    exceptionDisplayName,
                    unusableReason));
        }

        var codeExpression = GuardSymbolFacts.ToCodeExpression(attribute.ConstructorArguments[0], codeEnum);
        var constructor = FindWidestConstructor(exception, exceptionBase);

        // A type that derives from ErrorCodedException derives from System.Exception, so the depth
        // is known by now: the check above could not have passed without 'System.Exception' being
        // resolvable, since ErrorCodedException itself would not have resolved either.
        var depth = GuardSymbolFacts.GetExceptionDepth(exception, exceptionBase);
        Debug.Assert(
            depth is not null,
            "An ErrorCodedException-derived type must have a known distance to System.Exception.");

        return new ErrorCodeExceptionCandidate(
            IsValid: true,
            TCodeFqn: SymbolFacts.ToFqn(codeEnum),
            TCodeDisplayName: codeEnum.ToDisplayString(),
            ExceptionFqn: SymbolFacts.ToFqn(exception),
            ExceptionDisplayName: exceptionDisplayName,
            ExceptionName: exception.Name,
            ExceptionFlattenedName: GuardSymbolFacts.ToFlattenedIdentifier(exception),
            CodeExpression: codeExpression,
            CodeDisplayName: GuardSymbolFacts.ToCodeDisplayName(codeExpression, codeEnum),
            InheritanceDepth: depth.GetValueOrDefault(),
            Constructor: constructor.Shape,
            MessageIsNullable: constructor.MessageIsNullable,
            InnerIsNullable: constructor.InnerIsNullable,
            IsEffectivelyPublic: SymbolFacts.IsEffectivelyPublic(exception),
            Location: location,
            Diagnostic: null);
    }

    /// <summary>
    /// Returns the clause naming why the generated code could not name or construct the exception,
    /// or <see langword="null"/> when it can.
    /// </summary>
    private static string? GetUnusableReason(INamedTypeSymbol exception)
    {
        if (exception.IsAbstract)
        {
            return "abstract";
        }

        if (exception.Arity > 0)
        {
            return "generic";
        }

        return SymbolFacts.IsGenericOrNestedInGeneric(exception) ? "nested inside a generic type" : null;
    }

    /// <summary>
    /// Picks the widest of the three recognised public constructor shapes the exception declares.
    /// </summary>
    /// <remarks>
    /// One shape, not one per constructor: the exception gets a single factory and a single throw
    /// helper, mirroring its widest recognised constructor. Anything narrower is reachable through
    /// that one's defaults, and anything the recognised shapes do not cover -- a constructor taking
    /// domain-specific parameters -- has no mirror at all and is constructed the ordinary way.
    /// </remarks>
    private static ConstructorFacts FindWidestConstructor(INamedTypeSymbol exception, INamedTypeSymbol? exceptionBase)
    {
        var widest = new ConstructorFacts(ConstructorShape.None, messageIsNullable: true, innerIsNullable: true);

        foreach (var constructor in exception.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var facts = Recognise(constructor, exceptionBase);
            if (facts.Shape > widest.Shape)
            {
                widest = facts;
            }
        }

        return widest;
    }

    private static ConstructorFacts Recognise(IMethodSymbol constructor, INamedTypeSymbol? exceptionBase)
    {
        var parameters = constructor.Parameters;

        if (parameters.Length == 0)
        {
            return new ConstructorFacts(ConstructorShape.Parameterless, messageIsNullable: true, innerIsNullable: true);
        }

        // 'ref'/'out'/'in' is not a recognised shape: the helper passes an argument by value, and
        // mirroring the parameter type without its ref kind would emit a call the compiler rejects
        // (CS1620) inside a file the user cannot edit.
        if (parameters[0].Type.SpecialType != SpecialType.System_String || !IsByValue(parameters[0]))
        {
            return new ConstructorFacts(ConstructorShape.None, messageIsNullable: true, innerIsNullable: true);
        }

        var messageIsNullable = IsNullable(parameters[0]);

        if (parameters.Length == 1)
        {
            return new ConstructorFacts(ConstructorShape.Message, messageIsNullable, innerIsNullable: true);
        }

        // Exactly 'System.Exception', not a derived type: the helper passes whatever the caller
        // hands it, and narrowing the parameter would make the generated signature a lie.
        if (parameters.Length != 2
            || !SymbolEqualityComparer.Default.Equals(parameters[1].Type, exceptionBase)
            || !IsByValue(parameters[1]))
        {
            return new ConstructorFacts(ConstructorShape.None, messageIsNullable: true, innerIsNullable: true);
        }

        return new ConstructorFacts(ConstructorShape.MessageAndInner, messageIsNullable, IsNullable(parameters[1]));
    }

    /// <summary>
    /// Whether the parameter is passed by value, which is the only way the generated helper passes
    /// anything.
    /// </summary>
    private static bool IsByValue(IParameterSymbol parameter) => parameter.RefKind == RefKind.None;

    /// <summary>
    /// Whether the generated helper may pass <see langword="null"/> for the parameter without a
    /// nullable-reference warning in the consumer's build. An oblivious parameter -- one from a
    /// compilation unit with nullable annotations disabled -- counts as nullable, which is what
    /// passing null to it actually does.
    /// </summary>
    private static bool IsNullable(IParameterSymbol parameter) =>
        parameter.NullableAnnotation != NullableAnnotation.NotAnnotated;

    private readonly struct ConstructorFacts
    {
        public ConstructorFacts(ConstructorShape shape, bool messageIsNullable, bool innerIsNullable)
        {
            Shape = shape;
            MessageIsNullable = messageIsNullable;
            InnerIsNullable = innerIsNullable;
        }

        public ConstructorShape Shape { get; }

        public bool MessageIsNullable { get; }

        public bool InnerIsNullable { get; }
    }
}
