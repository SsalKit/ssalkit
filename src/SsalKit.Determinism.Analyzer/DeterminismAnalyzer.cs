using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SsalKit.Determinism.Analyzer.Diagnostics;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Determinism.Analyzer;

/// <summary>
/// Reports <c>SSALD001</c>-<c>SSALD008</c> for non-deterministic APIs used directly inside a
/// <c>[SsalKit.Determinism.Deterministic]</c> scope, and for the markings around it that are
/// missing or do nothing.
/// </summary>
/// <remarks>
/// <para>
/// Two things distinguish this from a general banned-API list. The scope is <b>opt-in</b>: nothing
/// is reported outside a <c>[Deterministic]</c> type or member, which is what lets a deterministic
/// simulation core and the logging, UI, and composition-root code around it live in one project.
/// And every message names a <b>concrete replacement</b> from the SsalKit family rather than only
/// stating that the API is banned.
/// </para>
/// <para>
/// The analysis is shallow on purpose: it sees the four operation kinds through which a banned
/// member can be named directly, and nothing else. A call that reaches a banned API through an
/// unmarked helper is invisible here, and no interprocedural propagation is planned -- "shallow and
/// predictable" is the product, not a limitation waiting to be lifted. Silence is therefore not a
/// proof of determinism.
/// </para>
/// <para>
/// <c>[Deterministic(Strict = true)]</c> and SSALD008 are the answer to that limitation that does
/// not deepen the analysis: instead of following a call into an unmarked helper, they report the
/// call itself, on the grounds that the helper sits under no <c>[Deterministic]</c> marking and so
/// is never analyzed by anything. The callee's body is never read, the check is exactly one hop, and
/// the same four operation kinds carry it -- which is why it is a second question asked on the way
/// out of the same walk rather than a second analysis.
/// </para>
/// <para>
/// Ordering matters for cost: the scope test runs <em>first</em> and returns immediately when the
/// operation is outside every scope, so in a codebase that uses no <c>[Deterministic]</c> at all the
/// per-operation work is one containing-symbol walk that terminates at the first unmarked type.
/// Compilations that do not reference the runtime package at all register no operation actions
/// whatsoever.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// How a banned member is named in a diagnostic message: the declaring type (with its containing
    /// types, but without its namespace) and the member name, which is how the call reads at the
    /// site that triggered the diagnostic.
    /// </summary>
    private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    private const string GeneratedCodeAttributeName = "GeneratedCodeAttribute";
    private const string GeneratedCodeAttributeNamespace = "System.CodeDom.Compiler";

    /// <summary>
    /// The file-name conventions that mark a source file as tool-written, matching the set the
    /// compiler's own generated-code detection recognizes.
    /// </summary>
    private static readonly string[] GeneratedFileSuffixes = [".g.cs", ".g.i.cs", ".generated.cs", ".designer.cs"];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.AmbientTime,
        DiagnosticDescriptors.NonDeterministicRandomness,
        DiagnosticDescriptors.GuidGeneration,
        DiagnosticDescriptors.RandomizedHashing,
        DiagnosticDescriptors.EnvironmentIdentity,
        DiagnosticDescriptors.SchedulingAndParallelism,
        DiagnosticDescriptors.OrphanAllowNonDeterminism,
        DiagnosticDescriptors.UnmarkedCallFromStrictScope);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        // Generated code is analyzed and reported on: a source generator that emits into a user's
        // [Deterministic] partial type produces code that runs inside the deterministic core just
        // like hand-written code, so a non-deterministic call there is the same bug.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var attributes = ScopeAttributes.Resolve(compilationStartContext.Compilation);

            if (attributes is null)
            {
                // The runtime package isn't referenced by this compilation, so no [Deterministic]
                // scope can exist in it; nothing to analyze.
                return;
            }

            var catalog = BannedApiCatalog.Create(compilationStartContext.Compilation);

            // Registered unconditionally, even for an empty catalog: SSALD008 is about markings
            // rather than about banned APIs, so gating registration on the catalog would make strict
            // mode disappear in a compilation where nothing in the catalog resolved. The catalog is
            // consulted inside the report path instead, after the scope test that costs nothing
            // outside a scope.
            compilationStartContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, attributes, catalog),
                OperationKind.Invocation);

            compilationStartContext.RegisterOperationAction(
                operationContext => AnalyzePropertyReference(operationContext, attributes, catalog),
                OperationKind.PropertyReference);

            compilationStartContext.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, attributes, catalog),
                OperationKind.ObjectCreation);

            compilationStartContext.RegisterOperationAction(
                operationContext => AnalyzeMethodReference(operationContext, attributes, catalog),
                OperationKind.MethodReference);

            compilationStartContext.RegisterSymbolAction(
                symbolContext => AnalyzeOrphanExemption(symbolContext, attributes),
                SymbolKind.NamedType,
                SymbolKind.Method,
                SymbolKind.Property);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context, ScopeAttributes attributes, BannedApiCatalog catalog) =>
        ReportIfBanned(context, attributes, catalog, ((IInvocationOperation)context.Operation).TargetMethod);

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context, ScopeAttributes attributes, BannedApiCatalog catalog) =>
        ReportIfBanned(context, attributes, catalog, ((IPropertyReferenceOperation)context.Operation).Property);

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context, ScopeAttributes attributes, BannedApiCatalog catalog) =>
        // A struct's implicit parameterless constructor can come back as null; there is nothing to
        // look up then.
        ReportIfBanned(context, attributes, catalog, ((IObjectCreationOperation)context.Operation).Constructor);

    private static void AnalyzeMethodReference(
        OperationAnalysisContext context, ScopeAttributes attributes, BannedApiCatalog catalog) =>
        ReportIfBanned(context, attributes, catalog, ((IMethodReferenceOperation)context.Operation).Method);

    private static void ReportIfBanned(
        OperationAnalysisContext context, ScopeAttributes attributes, BannedApiCatalog catalog, ISymbol? referenced)
    {
        if (referenced is null)
        {
            return;
        }

        // Scope first, catalog second (design §5.2): outside a scope this costs one walk up the
        // containing-symbol chain and nothing else.
        if (!DeterministicScope.IsInsideDeterministicScope(context.ContainingSymbol, attributes, out var strict))
        {
            return;
        }

        if (IsInsideNameOf(context.Operation))
        {
            // nameof(DateTime.UtcNow) names a member, it does not read one: the whole expression is
            // a compile-time constant. Roslyn still builds a member-reference operation for the
            // argument, so it has to be excluded here rather than by never being visited.
            return;
        }

        var descriptor = catalog.IsEmpty ? null : catalog.Find(referenced);

        if (descriptor is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor, context.Operation.Syntax.GetLocation(), Describe(referenced)));

            // The catalog wins: one reference produces at most one diagnostic, and the specific
            // rule is always the more useful of the two. In practice every catalog entry lives in
            // another assembly and so could never reach SSALD008 anyway, but fixing the order here
            // means a future entry cannot start double-reporting.
            return;
        }

        if (strict)
        {
            ReportIfUnmarked(context, attributes, referenced);
        }
    }

    /// <summary>
    /// The SSALD008 path: a reference from a strict scope to a member of this assembly that no
    /// <c>[Deterministic]</c> marking covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The coverage test is <see cref="DeterministicScope.HasDeterministicMarkingInChain"/> -- the
    /// same question SSALD007 asks, asked here about the callee instead of about an attribute
    /// application. A callee's own <c>[AllowNonDeterminism]</c> does <em>not</em> silence this:
    /// standing on its own it is an orphan that SSALD007 reports in its own right, and letting it
    /// count would both make SSALD007's "suppresses nothing" a lie and let one declaration exempt
    /// every strict call site in the assembly without leaving a trace at any of them. An exemption
    /// nested inside a <c>[Deterministic]</c> type is a different thing and is silent here, because
    /// the marking above it is the coverage this rule is looking for.
    /// </para>
    /// <para>
    /// Reached only from inside a strict scope and only after the catalog has passed on the symbol.
    /// The tests below run cheapest first: three facts already on the symbol, then a walk up the
    /// callee's containing-symbol chain, and only for what survives that, the two that have to read
    /// syntax.
    /// </para>
    /// </remarks>
    private static void ReportIfUnmarked(
        OperationAnalysisContext context, ScopeAttributes attributes, ISymbol referenced)
    {
        // OriginalDefinition throughout, matching how the catalog identifies a member: it is what
        // makes a constructed generic and a reduced extension-method call resolve to the one
        // declaration an attribute could be written on.
        var callee = referenced.OriginalDefinition;

        if (MarkableContainingType(callee, context.Compilation) is not { } markable
            || DeterministicScope.HasDeterministicMarkingInChain(callee, attributes)
            || IsGeneratedCode(callee)
            || !HasAnalyzableBody(callee))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnmarkedCallFromStrictScope,
            context.Operation.Syntax.GetLocation(),
            Describe(callee),
            markable.ToDisplayString(TypeFormat)));
    }

    /// <summary>
    /// The type an attribute would go on if <paramref name="callee"/> is a member this compilation's
    /// author can mark at all, or <see langword="null"/> when no such place exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every exclusion here is the same one: reporting a member nobody can put an attribute on
    /// produces a warning with no fix behind it, which is the fastest way to make an opt-in rule
    /// unusable. Another assembly's members are not this author's source; an interface member cannot
    /// carry <c>[Deterministic]</c> at all, since the attribute has no <c>Interface</c> target; and
    /// an implicitly declared member has no declaration to write on.
    /// </para>
    /// <para>
    /// The implicit-declaration test covers a record's <c>Equals</c>, <c>GetHashCode</c>,
    /// <c>ToString</c>, <c>Deconstruct</c> and clone member, an implicit parameterless constructor,
    /// and a delegate's <c>Invoke</c>. It does <em>not</em> cover a positional record's properties or
    /// its primary constructor, which Roslyn reports as explicitly declared because they point at
    /// the record header -- those are excluded further down, by
    /// <see cref="HasAnalyzableBody(ISymbol)"/>, on the grounds that no code was written behind them
    /// either.
    /// </para>
    /// </remarks>
    private static INamedTypeSymbol? MarkableContainingType(ISymbol callee, Compilation compilation)
    {
        if (callee.IsImplicitlyDeclared
            || !SymbolEqualityComparer.Default.Equals(callee.ContainingAssembly, compilation.Assembly))
        {
            return null;
        }

        return callee.ContainingType is { TypeKind: not TypeKind.Interface } containingType ? containingType : null;
    }

    /// <summary>
    /// Whether <paramref name="callee"/> was written by a tool rather than by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same test as everything in <see cref="MarkableContainingType"/>, applied to the one case
    /// that is in this assembly and still out of reach: a source generator's output is not a file
    /// anyone can add an attribute to. This is not a rare shape either -- a generated extension
    /// class is exactly the kind of helper a deterministic core calls, and reporting one would leave
    /// a warning whose only fix is to stop using the generator.
    /// </para>
    /// <para>
    /// A generator that emits <em>into</em> a marked <c>partial</c> type is unaffected: those
    /// members' containing-symbol chain runs through the user's own marking, so they were already
    /// silent before reaching here. Note also that this is the opposite axis from
    /// <c>ConfigureGeneratedCodeAnalysis</c>, which decides whether generated code is analyzed as a
    /// <em>call site</em> -- it is, and stays so, because a banned call emitted into a deterministic
    /// core is the same bug as a hand-written one.
    /// </para>
    /// <para>
    /// The recognition follows the conventions the compiler and every other analyzer already use:
    /// the <c>[GeneratedCode]</c> attribute on the member or any type containing it, and the
    /// generated-file naming conventions. Every declaration has to be generated for the member to
    /// count as generated, so a <c>partial</c> method whose implementation is hand-written stays
    /// reportable.
    /// </para>
    /// </remarks>
    private static bool IsGeneratedCode(ISymbol callee)
    {
        for (ISymbol? current = callee; current is not null; current = current.ContainingType)
        {
            if (HasGeneratedCodeAttribute(current))
            {
                return true;
            }
        }

        var references = callee.DeclaringSyntaxReferences;

        if (references.Length == 0)
        {
            return false;
        }

        foreach (var reference in references)
        {
            if (!IsGeneratedFilePath(reference.SyntaxTree.FilePath))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasGeneratedCodeAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: GeneratedCodeAttributeName } attributeClass
                && attributeClass.ContainingNamespace.ToDisplayString() == GeneratedCodeAttributeNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedFilePath(string path)
    {
        foreach (var suffix in GeneratedFileSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="callee"/> has a body of its own that a marking would bring into the
    /// analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What SSALD008 reports is code running unanalyzed inside a deterministic core. Where no code
    /// was written, there is nothing to ask for: an auto-implemented property reads a field, and an
    /// <c>abstract</c>, <c>extern</c> or never-implemented <c>partial</c> declaration is a signature
    /// whose real target this analysis could not resolve anyway.
    /// </para>
    /// <para>
    /// Recognizing that means reading syntax, which is why this test runs last -- only for a
    /// reference already past every other exclusion, never once per operation.
    /// </para>
    /// </remarks>
    private static bool HasAnalyzableBody(ISymbol callee)
    {
        if (callee is IPropertySymbol property)
        {
            return HasBody(property.GetMethod) || HasBody(property.SetMethod);
        }

        return callee is IMethodSymbol method && HasBody(method);
    }

    private static bool HasBody(IMethodSymbol? method)
    {
        if (method is null || method.IsAbstract || method.IsExtern)
        {
            return false;
        }

        // A partial declaration carries the attributes of both its parts, but only the
        // implementation part carries the body -- and a definition nobody implemented has none.
        var declaration = method.IsPartialDefinition ? method.PartialImplementationPart : method;

        if (declaration is null || declaration.DeclaringSyntaxReferences.Length == 0)
        {
            // No source to look at. P1 already excluded other assemblies; this is the belt to that
            // brace, since a symbol can be synthesized without being flagged implicit.
            return false;
        }

        return DeclarationHasBody(declaration.DeclaringSyntaxReferences[0].GetSyntax());
    }

    /// <summary>
    /// Whether a declaration is one of the forms code can actually be written in, and has code in
    /// it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a whitelist rather than a list of exclusions, because the interesting cases are the
    /// ones where a symbol's declaration is not a declaration of its own at all. A positional
    /// record's property points at the parameter in the record header, and a primary constructor
    /// points at the type declaration: both are members Roslyn reports as explicitly declared --
    /// <see cref="ISymbol.IsImplicitlyDeclared"/> is <see langword="false"/> for them, unlike the
    /// record's <c>Equals</c> or <c>Deconstruct</c> -- yet neither has a line of user code behind
    /// it. Asking a consumer to mark a record because something read one of its properties is
    /// exactly the noise that would make this rule unusable in code that uses records to carry data.
    /// </para>
    /// <para>
    /// The forms that do hold code are few, and each is only counted when it actually carries a
    /// body: a bare <c>get;</c>, an <c>abstract</c> signature and an unimplemented <c>partial</c>
    /// declaration all reach this and all say no.
    /// </para>
    /// </remarks>
    private static bool DeclarationHasBody(SyntaxNode declaration) => declaration switch
    {
        AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,

        // `int X => ...`: the getter's own declaration is the arrow clause.
        ArrowExpressionClauseSyntax => true,

        PropertyDeclarationSyntax property => property.ExpressionBody is not null,

        // Methods, constructors, operators, and finalizers.
        BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,

        LocalFunctionStatementSyntax local => local.Body is not null || local.ExpressionBody is not null,

        _ => false,
    };

    private static bool IsInsideNameOf(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current.Kind == OperationKind.NameOf)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeOrphanExemption(SymbolAnalysisContext context, ScopeAttributes attributes)
    {
        var symbol = context.Symbol;

        if (!DeterministicScope.TryGetAllowNonDeterminism(symbol, attributes, out var attributeData)
            || DeterministicScope.HasDeterministicMarkingInChain(symbol, attributes))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.OrphanAllowNonDeterminism,
            AttributeLocations.GetLocation(attributeData, symbol),
            Describe(symbol)));
    }

    /// <summary>
    /// How a symbol reads in a diagnostic message.
    /// </summary>
    /// <remarks>
    /// A constructor is written the way it is called (<c>new Random</c>) rather than by its
    /// <c>.ctor</c> metadata name; everything else is named by its declaring type and its own name.
    /// The namespace is left out deliberately: the message has to be readable at a glance, and the
    /// type names in this catalog (<c>DateTime</c>, <c>Guid</c>, <c>HashCode</c>) are unambiguous
    /// without it.
    /// </remarks>
    private static string Describe(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol type)
        {
            return type.ToDisplayString(TypeFormat);
        }

        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
        {
            return "new " + constructor.ContainingType.ToDisplayString(TypeFormat);
        }

        return symbol.ContainingType is null
            ? symbol.Name
            : symbol.ContainingType.ToDisplayString(TypeFormat) + "." + symbol.Name;
    }
}
