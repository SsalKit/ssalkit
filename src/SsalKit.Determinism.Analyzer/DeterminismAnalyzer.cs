using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SsalKit.Determinism.Analyzer.Diagnostics;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Determinism.Analyzer;

/// <summary>
/// Reports <c>SSALD001</c>-<c>SSALD007</c> for non-deterministic APIs used directly inside a
/// <c>[SsalKit.Determinism.Deterministic]</c> scope.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.AmbientTime,
        DiagnosticDescriptors.NonDeterministicRandomness,
        DiagnosticDescriptors.GuidGeneration,
        DiagnosticDescriptors.RandomizedHashing,
        DiagnosticDescriptors.EnvironmentIdentity,
        DiagnosticDescriptors.SchedulingAndParallelism,
        DiagnosticDescriptors.OrphanAllowNonDeterminism);

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

            if (!catalog.IsEmpty)
            {
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
            }

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
        if (!DeterministicScope.IsInsideDeterministicScope(context.ContainingSymbol, attributes))
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

        var descriptor = catalog.Find(referenced);

        if (descriptor is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor, context.Operation.Syntax.GetLocation(), Describe(referenced)));
    }

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
