using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// Reports <c>MINI900</c> on any type whose name starts with <c>Bad</c>, at the symbol's real
/// location -- so unlike <see cref="MiniGenerator"/>'s diagnostics, this one carries its syntax
/// tree and <see cref="DiagnosticAssert.LocatedOn"/> can find the source on its own.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BadNameAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "MINI900",
        "Type is named Bad",
        "Type '{0}' must not be named Bad",
        "MiniAnalyzer",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(
            static context =>
            {
                if (context.Symbol.Name.StartsWith("Bad", StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, context.Symbol.Locations[0], context.Symbol.Name));
                }
            },
            SymbolKind.NamedType);
    }
}

/// <summary>
/// Reports <c>MINI901</c> on any type whose name starts with <c>Odd</c>. Its whole purpose is to be
/// run alongside <see cref="BadNameAnalyzer"/>, which is how a package's analyzers actually run.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OddNameAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "MINI901",
        "Type is named Odd",
        "Type '{0}' is named Odd",
        "MiniAnalyzer",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(
            static context =>
            {
                if (context.Symbol.Name.StartsWith("Odd", StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, context.Symbol.Locations[0], context.Symbol.Name));
                }
            },
            SymbolKind.NamedType);
    }
}
