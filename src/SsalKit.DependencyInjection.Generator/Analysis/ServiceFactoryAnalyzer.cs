using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SsalKit.DependencyInjection.Generator.Diagnostics;
using SsalKit.DependencyInjection.Generator.Parsing;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Analysis;

/// <summary>
/// Reports diagnostics SSAL016-SSAL020 for invalid uses of
/// <c>[SsalKit.DependencyInjection.ServiceFactory]</c>.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="ServiceAttributeAnalyzer"/> rather than folded into it: the two
/// attributes share no state and no cross-symbol (CompilationEnd) rules, so a per-symbol analyzer
/// of its own is both simpler and cheaper -- it does nothing at all in a compilation that never
/// references the attribute. Validation itself lives in <see cref="ServiceFactoryValidator"/>,
/// shared with <see cref="ServiceFactoryParser"/>, so what is reported here and what the generator
/// refuses to emit can never drift apart.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceFactoryAnalyzer : DiagnosticAnalyzer
{
    private const string ServiceFactoryAttributeMetadataName = "SsalKit.DependencyInjection.ServiceFactoryAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.ServiceFactoryTargetNotInterface,
        DiagnosticDescriptors.ServiceFactoryMemberShapeInvalid,
        DiagnosticDescriptors.ServiceFactoryMethodSignatureInvalid,
        DiagnosticDescriptors.ServiceFactoryGenericNotSupported,
        DiagnosticDescriptors.ServiceFactoryInaccessibleType);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var attributeSymbol = compilationStartContext.Compilation.GetTypeByMetadataName(ServiceFactoryAttributeMetadataName);
            if (attributeSymbol is null)
            {
                // The attribute assembly isn't referenced by this compilation; nothing to analyze.
                return;
            }

            compilationStartContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, attributeSymbol),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol attributeSymbol)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        var attributeData = FindServiceFactoryAttribute(typeSymbol, attributeSymbol);
        if (attributeData is null)
        {
            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();

        var validation = ServiceFactoryValidator.Validate(typeSymbol, context.Compilation);
        if (validation.Kind == ServiceFactoryValidationKind.Success)
        {
            return;
        }

        var location = AttributeLocations.GetLocation(attributeData, typeSymbol);
        var typeFqn = SymbolFacts.ToFqn(typeSymbol);

        switch (validation.Kind)
        {
            case ServiceFactoryValidationKind.NotAnInterface:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ServiceFactoryTargetNotInterface, location, typeFqn, validation.Detail));
                break;

            case ServiceFactoryValidationKind.GenericNotSupported:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ServiceFactoryGenericNotSupported, location, typeFqn, validation.Detail));
                break;

            case ServiceFactoryValidationKind.MemberShapeInvalid:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ServiceFactoryMemberShapeInvalid, location, typeFqn, validation.Detail));
                break;

            case ServiceFactoryValidationKind.SignatureInvalid:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ServiceFactoryMethodSignatureInvalid,
                    location,
                    typeFqn,
                    validation.Method!.Name,
                    validation.Detail));
                break;

            case ServiceFactoryValidationKind.Inaccessible:
            default:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ServiceFactoryInaccessibleType, location, validation.Detail, typeFqn));
                break;
        }
    }

    private static AttributeData? FindServiceFactoryAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attributeData in typeSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, attributeSymbol))
            {
                return attributeData;
            }
        }

        return null;
    }
}
