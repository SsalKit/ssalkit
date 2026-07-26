using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Converts an interface decorated with <c>[ServiceFactory]</c> into an equatable
/// <see cref="ServiceFactoryModel"/>, dropping any interface that <c>ServiceFactoryAnalyzer</c>
/// would report (both go through <see cref="ServiceFactoryValidator"/>), so the generator never
/// emits an implementation that would not compile.
/// </summary>
/// <remarks>
/// As with <see cref="ServiceAttributeParser"/>, only primitive data leaves this method: no
/// <see cref="ISymbol"/>, <see cref="Compilation"/>, or syntax node is retained in the returned
/// model, which is what lets the incremental generator cache correctly.
/// </remarks>
internal static class ServiceFactoryParser
{
    /// <summary>
    /// The namespace root every generated factory implementation is emitted into. Reserved for the
    /// generator: consumer code is not expected to declare types under it, and
    /// <see cref="GeneratedOutputRecognizer"/> uses it to keep the analyzers from treating this
    /// generator's own output as consumer code.
    /// </summary>
    private const string GeneratedNamespaceRoot = GeneratedOutputRecognizer.GeneratedNamespaceRoot;

    /// <summary>
    /// <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/> plus nullable reference type
    /// annotations, used for the two types that appear in the generated method's <em>declaration</em>
    /// (its return type and its parameter type).
    /// </summary>
    /// <remarks>
    /// The plain fully-qualified format drops <c>?</c> annotations entirely, so an interface
    /// declaring <c>IList&lt;string?&gt; Create(Kind kind)</c> would be implemented as
    /// <c>IList&lt;string&gt; Create(...)</c> -- a nullability mismatch the compiler reports as
    /// CS8613/CS8766 inside a file the consumer cannot edit, and an error outright under
    /// <c>TreatWarningsAsErrors</c>. Only the factory's signature types use this format; every other
    /// name the generator emits identifies a type rather than declaring a member's nullability, so
    /// annotating those would change existing output for no benefit.
    /// </remarks>
    private static readonly SymbolDisplayFormat SignatureFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Distinguishes the generated class from the interface it implements without needing to
    /// rewrite the interface's name (e.g. by stripping a leading <c>I</c>), which would let two
    /// differently-named interfaces collide.
    /// </summary>
    private const string ImplementationSuffix = "Implementation";

    public static ServiceFactoryModel? GetModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // SSAL016-SSAL020: every rejection the analyzer reports drops the interface here instead.
        var validation = ServiceFactoryValidator.Validate(typeSymbol, context.SemanticModel.Compilation);
        if (validation.Kind != ServiceFactoryValidationKind.Success)
        {
            return null;
        }

        var method = validation.Method!;
        var interfaceTypeFqn = SymbolFacts.ToFqn(typeSymbol);
        var implementationNamespace = GetImplementationNamespace(typeSymbol);
        var implementationTypeName = GetImplementationTypeName(typeSymbol);

        return new ServiceFactoryModel(
            InterfaceTypeFqn: interfaceTypeFqn,
            ImplementationNamespace: implementationNamespace,
            ImplementationTypeName: implementationTypeName,
            ImplementationTypeFqn: $"global::{implementationNamespace}.{implementationTypeName}",
            MethodName: CSharpNaming.EscapeKeyword(method.Name),
            ParameterName: CSharpNaming.EscapeKeyword(method.Parameters[0].Name),
            ParameterTypeFqn: validation.KeyType!.ToDisplayString(SignatureFormat),
            ReturnTypeFqn: validation.ReturnType!.ToDisplayString(SignatureFormat),
            // GetRequiredKeyedService<T> constrains T to `notnull`, so a top-level `?` on the
            // service type has to come off before it is used as the type argument (CS8714
            // otherwise). Only the top level: an annotation *inside* the type, as in
            // `IList<string?>`, is part of the type's identity here and dropping it would make the
            // returned value's type disagree with the declared one (CS8619). The non-null result is
            // implicitly convertible to the nullable return type, so the method still type-checks.
            LookupTypeFqn: validation.ReturnType!
                .WithNullableAnnotation(NullableAnnotation.NotAnnotated)
                .ToDisplayString(SignatureFormat),
            HintName: HintNameSanitizer.Sanitize($"{interfaceTypeFqn}.ServiceFactory"));
    }

    /// <summary>
    /// Reproduces everything that qualifies the interface's name -- its namespace <em>and</em> its
    /// containing-type chain -- as namespace segments underneath
    /// <see cref="GeneratedNamespaceRoot"/>, so that <c>A.B.IFoo</c>, <c>A.C.IFoo</c>, and a nested
    /// <c>A.B.IFoo</c> all land on distinct fully-qualified names.
    /// </summary>
    /// <remarks>
    /// Turning containing types into namespace segments (rather than flattening them into the class
    /// name with a separator) is what makes the result collision-free rather than merely unlikely to
    /// collide: two factories can only produce the same generated name if their own qualified names
    /// were identical, which C# already rejects -- a namespace and a type cannot share a
    /// fully-qualified name either. The generated class is deliberately never <em>nested</em> in a
    /// real containing type: that would require every one of those types to be <c>partial</c>.
    /// </remarks>
    private static string GetImplementationNamespace(INamedTypeSymbol typeSymbol)
    {
        var segments = new List<string>();

        var containingNamespace = typeSymbol.ContainingNamespace;
        if (containingNamespace is { IsGlobalNamespace: false })
        {
            // FullyQualifiedFormat escapes any namespace segment that is a reserved keyword; only
            // its "global::" prefix has to be removed, since the result is being nested under
            // another namespace rather than used as a qualified name on its own.
            var declared = SymbolFacts.ToFqn(containingNamespace);
            const string globalPrefix = "global::";
            if (declared.StartsWith(globalPrefix, StringComparison.Ordinal))
            {
                declared = declared.Substring(globalPrefix.Length);
            }

            segments.Add(declared);
        }

        var containingTypeIndex = segments.Count;
        for (var containing = typeSymbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            // A type name is a valid namespace segment, but -- unlike the namespace text above --
            // it arrives unescaped, so a type literally named `@class` needs its `@` put back.
            segments.Insert(containingTypeIndex, CSharpNaming.EscapeKeyword(containing.Name));
        }

        return segments.Count == 0
            ? GeneratedNamespaceRoot
            : GeneratedNamespaceRoot + "." + CSharpNaming.JoinIdentifierSegments(segments, '.');
    }

    /// <summary>
    /// The interface's own simple name plus <see cref="ImplementationSuffix"/>. Everything that
    /// qualified the interface -- namespace and containing types alike -- is carried by
    /// <see cref="GetImplementationNamespace"/> instead.
    /// </summary>
    private static string GetImplementationTypeName(INamedTypeSymbol typeSymbol) =>
        // Never a keyword and never starts with a digit: the suffix is appended to a C# type name,
        // which is already a valid identifier.
        typeSymbol.Name + ImplementationSuffix;
}
