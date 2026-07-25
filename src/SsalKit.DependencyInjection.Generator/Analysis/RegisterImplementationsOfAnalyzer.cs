using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SsalKit.DependencyInjection.Generator.Diagnostics;
using SsalKit.DependencyInjection.Generator.Parsing;

namespace SsalKit.DependencyInjection.Generator.Analysis;

/// <summary>
/// Reports diagnostics SSAL021-SSAL026 for invalid, redundant, or fruitless uses of
/// <c>[assembly: SsalKit.DependencyInjection.RegisterImplementationsOf]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds of rule live here, reported from two different kinds of action. Everything decidable
/// from the declaration alone (SSAL021, SSAL023, SSAL024, SSAL025) is reported from a syntax node
/// action on the attribute itself, so those errors behave like ordinary live diagnostics rather
/// than build-only ones. Everything that depends on what the scan actually found (SSAL022, SSAL026)
/// necessarily waits for every type in the compilation to have been seen, and is reported from a
/// compilation-end action -- both descriptors carry
/// <see cref="WellKnownDiagnosticTags.CompilationEnd"/> accordingly.
/// </para>
/// <para>
/// The matching itself is <see cref="ConventionImplementationMatcher"/>'s, shared verbatim with the
/// generator's <see cref="ConventionScanner"/>, so "the analyzer says this contract matched
/// nothing" and "the generator emitted no registration for it" can never disagree.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegisterImplementationsOfAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.ContractNotInterface,
        DiagnosticDescriptors.ContractMatchedNothing,
        DiagnosticDescriptors.DuplicateContract,
        DiagnosticDescriptors.UndefinedContractEnumValue,
        DiagnosticDescriptors.ContractInaccessibleType,
        DiagnosticDescriptors.ConflictingContractRegistrations);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var attributeSymbol = compilationStartContext.Compilation
                .GetTypeByMetadataName(ContractDeclarationReader.AttributeMetadataName);

            if (attributeSymbol is null)
            {
                // The attribute assembly isn't referenced by this compilation; nothing to analyze.
                return;
            }

            var declarations = ContractDeclarationReader.Read(compilationStartContext.Compilation, attributeSymbol);
            if (declarations.IsEmpty)
            {
                return;
            }

            // Built once here and only read from the (concurrent) actions below.
            var declarationsBySyntax = BuildSyntaxLookup(declarations);

            compilationStartContext.RegisterSyntaxNodeAction(
                syntaxContext => ReportDeclarationDiagnostic(syntaxContext, declarationsBySyntax),
                SyntaxKind.Attribute);

            var validDeclarations = declarations
                .Where(declaration => declaration.Kind == ContractValidationKind.Valid)
                .ToImmutableArray();

            if (validDeclarations.IsEmpty)
            {
                return;
            }

            var serviceAttributeSymbol = compilationStartContext.Compilation
                .GetTypeByMetadataName(ContractDeclarationReader.ServiceAttributeMetadataName);

            var matches = new ConcurrentBag<MatchRecord>();

            compilationStartContext.RegisterSymbolAction(
                symbolContext => CollectMatches(symbolContext, validDeclarations, serviceAttributeSymbol, matches),
                SymbolKind.NamedType);

            compilationStartContext.RegisterCompilationEndAction(
                endContext => ReportScanDiagnostics(endContext, validDeclarations, matches));
        });
    }

    /// <summary>
    /// Reports the declaration-site rule (if any) broken by the attribute application at
    /// <see cref="SyntaxNodeAnalysisContext.Node"/>. Attributes that are not
    /// <c>[assembly: RegisterImplementationsOf]</c> applications are simply absent from the lookup.
    /// </summary>
    private static void ReportDeclarationDiagnostic(
        SyntaxNodeAnalysisContext context,
        ImmutableDictionary<(SyntaxTree Tree, TextSpan Span), ContractDeclaration> declarationsBySyntax)
    {
        if (!declarationsBySyntax.TryGetValue((context.Node.SyntaxTree, context.Node.Span), out var declaration))
        {
            return;
        }

        var location = context.Node.GetLocation();

        switch (declaration.Kind)
        {
            case ContractValidationKind.Valid:
                break;

            case ContractValidationKind.NotAnInterface:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractNotInterface, location, declaration.ContractFqn, declaration.Detail));
                break;

            case ContractValidationKind.UndefinedEnumValue:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UndefinedContractEnumValue, location, declaration.Detail, declaration.EnumTypeName));
                break;

            case ContractValidationKind.Inaccessible:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractInaccessibleType, location, declaration.ContractFqn));
                break;

            case ContractValidationKind.Duplicate:
            default:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateContract, location, declaration.ContractFqn));
                break;
        }
    }

    /// <summary>
    /// Records every (declaration, service type, implementation type) triple the analyzed type
    /// contributes, for the compilation-end rules to reason about.
    /// </summary>
    private static void CollectMatches(
        SymbolAnalysisContext context,
        ImmutableArray<ContractDeclaration> validDeclarations,
        INamedTypeSymbol? serviceAttributeSymbol,
        ConcurrentBag<MatchRecord> matches)
    {
        if (context.Symbol is not INamedTypeSymbol candidate
            || !ConventionImplementationMatcher.IsCandidate(candidate, serviceAttributeSymbol, context.Compilation))
        {
            return;
        }

        var implementationTypeFqn = ConventionImplementationMatcher.GetImplementationTypeFqn(candidate);

        for (var i = 0; i < validDeclarations.Length; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (var match in ConventionImplementationMatcher.Match(candidate, validDeclarations[i], context.Compilation))
            {
                matches.Add(new MatchRecord(i, match.ServiceTypeFqn, implementationTypeFqn));
            }
        }
    }

    private static void ReportScanDiagnostics(
        CompilationAnalysisContext context,
        ImmutableArray<ContractDeclaration> validDeclarations,
        ConcurrentBag<MatchRecord> matches)
    {
        var recorded = matches.ToList();

        ReportEmptyContracts(context, validDeclarations, recorded);
        ReportConflictingOverlaps(context, validDeclarations, recorded);
    }

    /// <summary>
    /// SSAL022: a contract that nothing in the assembly matched. Reported per declaration, so a
    /// typo in one contract is pointed at even when every other contract in the assembly worked.
    /// </summary>
    private static void ReportEmptyContracts(
        CompilationAnalysisContext context,
        ImmutableArray<ContractDeclaration> validDeclarations,
        List<MatchRecord> recorded)
    {
        var matchedDeclarations = new HashSet<int>();
        foreach (var record in recorded)
        {
            matchedDeclarations.Add(record.DeclarationIndex);
        }

        for (var i = 0; i < validDeclarations.Length; i++)
        {
            if (matchedDeclarations.Contains(i))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractMatchedNothing,
                GetLocation(validDeclarations[i]),
                validDeclarations[i].ContractFqn));
        }
    }

    /// <summary>
    /// SSAL026: one (service type, implementation type) pair produced by two or more contracts that
    /// do not agree on lifetime and mode.
    /// </summary>
    /// <remarks>
    /// Overlapping contracts are legitimate on their own -- an unbound <c>typeof(IHandler&lt;&gt;)</c>
    /// and a closed <c>typeof(IHandler&lt;int&gt;)</c> can both be meant -- and when they agree the
    /// generator simply collapses the duplicate statement, so there is nothing to say. It is only
    /// when they disagree that the result is a registration pair no declaration asked for, and
    /// which of the two wins is decided by Microsoft.Extensions.DependencyInjection rather than by
    /// anything in the source. Every contributing declaration is reported, since each is an equally
    /// valid place to resolve the disagreement.
    /// </remarks>
    private static void ReportConflictingOverlaps(
        CompilationAnalysisContext context,
        ImmutableArray<ContractDeclaration> validDeclarations,
        List<MatchRecord> recorded)
    {
        foreach (var group in recorded.GroupBy(record => (record.ServiceTypeFqn, record.ImplementationTypeFqn)))
        {
            var declarationIndexes = group
                .Select(record => record.DeclarationIndex)
                .Distinct()
                .OrderBy(index => index)
                .ToList();

            if (declarationIndexes.Count < 2)
            {
                continue;
            }

            var settings = declarationIndexes
                .Select(index => (validDeclarations[index].Lifetime, validDeclarations[index].Mode))
                .Distinct()
                .ToList();

            // Overlapping but in agreement: ConventionScanner collapses the duplicate statement and
            // the emitted result is exactly what a single declaration would have produced.
            if (settings.Count < 2)
            {
                continue;
            }

            foreach (var index in declarationIndexes)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConflictingContractRegistrations,
                    GetLocation(validDeclarations[index]),
                    group.Key.ServiceTypeFqn,
                    group.Key.ImplementationTypeFqn,
                    declarationIndexes.Count.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    private static ImmutableDictionary<(SyntaxTree Tree, TextSpan Span), ContractDeclaration> BuildSyntaxLookup(
        ImmutableArray<ContractDeclaration> declarations)
    {
        var builder = ImmutableDictionary.CreateBuilder<(SyntaxTree, TextSpan), ContractDeclaration>();

        foreach (var declaration in declarations)
        {
            var syntaxReference = declaration.Attribute.ApplicationSyntaxReference;
            if (syntaxReference is null)
            {
                continue;
            }

            // An assembly can only carry one attribute application per source span, so no key can
            // collide; indexer assignment rather than Add() keeps that from being load-bearing.
            builder[(syntaxReference.SyntaxTree, syntaxReference.Span)] = declaration;
        }

        return builder.ToImmutable();
    }

    private static Location GetLocation(in ContractDeclaration declaration)
    {
        var syntaxReference = declaration.Attribute.ApplicationSyntaxReference;
        return syntaxReference is null ? Location.None : syntaxReference.GetSyntax().GetLocation();
    }

    private readonly record struct MatchRecord(int DeclarationIndex, string ServiceTypeFqn, string ImplementationTypeFqn);
}
