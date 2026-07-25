using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;
using SsalKit.Guard.Generator.Diagnostics;
using SsalKit.Guard.Generator.Models;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// Turns one <c>[ErrorCode&lt;TCode&gt;]</c>-decorated class into the candidates the assembler joins
/// with the containers: one per attribute application, since a type may declare a code in more than
/// one code enum.
/// </summary>
internal static class ErrorCodeExceptionParser
{
    public static EquatableArray<ErrorCodeExceptionCandidate> GetCandidates(
        GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // [AttributeUsage(Class)] already rejects everything else at the application site.
        if (context.TargetSymbol is not INamedTypeSymbol exception)
        {
            return EquatableArray<ErrorCodeExceptionCandidate>.Empty;
        }

        var compilation = context.SemanticModel.Compilation;
        var exceptionBase = compilation.GetTypeByMetadataName("System.Exception");
        var errorCodedException = compilation.GetTypeByMetadataName(SymbolFacts.ErrorCodedExceptionMetadataName);

        var candidates = ImmutableArray.CreateBuilder<ErrorCodeExceptionCandidate>();

        foreach (var attribute in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var codeEnum = attribute.AttributeClass?.TypeArguments.Length == 1
                ? attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol
                : null;

            // The attribute's own 'where TCode : struct, Enum' constraint means anything else is
            // already a compiler error at the application site.
            if (codeEnum is null || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            candidates.Add(GetCandidate(attribute, exception, codeEnum, exceptionBase, errorCodedException));
        }

        return EquatableArray.Create(candidates.ToImmutable());
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
        if (!SymbolFacts.DerivesFrom(exception, errorCodedException))
        {
            return ErrorCodeExceptionCandidate.Invalid(
                location,
                new DiagnosticInfo(
                    DiagnosticDescriptors.ExceptionMustDeriveFromErrorCodedException, location, exceptionDisplayName));
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

        var codeExpression = SymbolFacts.ToCodeExpression(attribute.ConstructorArguments[0], codeEnum);
        var constructor = FindWidestConstructor(exception, exceptionBase);

        return new ErrorCodeExceptionCandidate(
            IsValid: true,
            TCodeFqn: SymbolFacts.ToFqn(codeEnum),
            TCodeDisplayName: codeEnum.ToDisplayString(),
            ExceptionFqn: SymbolFacts.ToFqn(exception),
            ExceptionDisplayName: exceptionDisplayName,
            ExceptionName: exception.Name,
            ExceptionFlattenedName: SymbolFacts.ToFlattenedIdentifier(exception),
            CodeExpression: codeExpression,
            CodeDisplayName: SymbolFacts.ToCodeDisplayName(codeExpression, codeEnum),
            // A type that derives from ErrorCodedException necessarily derives from Exception, so
            // the depth is always known here.
            InheritanceDepth: SymbolFacts.GetExceptionDepth(exception, exceptionBase) ?? 0,
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
    /// Picks the widest of the three recognised public constructor shapes the exception declares,
    /// so the generated helpers offer everything the exception itself offers.
    /// </summary>
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

        if (parameters[0].Type.SpecialType != SpecialType.System_String)
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
        if (parameters.Length != 2 || !SymbolEqualityComparer.Default.Equals(parameters[1].Type, exceptionBase))
        {
            return new ConstructorFacts(ConstructorShape.None, messageIsNullable: true, innerIsNullable: true);
        }

        return new ConstructorFacts(ConstructorShape.MessageAndInner, messageIsNullable, IsNullable(parameters[1]));
    }

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
