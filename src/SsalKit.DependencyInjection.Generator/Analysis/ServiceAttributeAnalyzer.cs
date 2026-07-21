using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SsalKit.DependencyInjection.Generator.Diagnostics;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.DependencyInjection.Generator.Parsing;

namespace SsalKit.DependencyInjection.Generator.Analysis;

/// <summary>
/// Reports diagnostics SSAL001-SSAL008 for invalid or conflicting uses of
/// <c>[SsalKit.DependencyInjection.Service]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string ServiceAttributeMetadataName = "SsalKit.DependencyInjection.ServiceAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.InvalidTargetType,
        DiagnosticDescriptors.AsTypeNotImplemented,
        DiagnosticDescriptors.GenericClassNotSupported,
        DiagnosticDescriptors.DuplicateRegistration,
        DiagnosticDescriptors.KeyedTryAddEnumerableNotSupported,
        DiagnosticDescriptors.SelfTryAddEnumerableNotSupported,
        DiagnosticDescriptors.InaccessibleType,
        DiagnosticDescriptors.UndefinedEnumValue);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var serviceAttributeSymbol = compilationStartContext.Compilation.GetTypeByMetadataName(ServiceAttributeMetadataName);
            if (serviceAttributeSymbol is null)
            {
                // The attribute assembly isn't referenced by this compilation; nothing to analyze.
                return;
            }

            var records = new ConcurrentBag<RegistrationRecord>();

            compilationStartContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, serviceAttributeSymbol, records),
                SymbolKind.NamedType);

            compilationStartContext.RegisterCompilationEndAction(
                endContext => ReportDuplicates(endContext, records));
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol serviceAttributeSymbol,
        ConcurrentBag<RegistrationRecord> records)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } classSymbol)
        {
            return;
        }

        var serviceAttributes = GetServiceAttributes(classSymbol, serviceAttributeSymbol);
        if (serviceAttributes.Count == 0)
        {
            return;
        }

        var implementationTypeFqn = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        foreach (var attributeData in serviceAttributes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            AnalyzeAttribute(context, classSymbol, attributeData, implementationTypeFqn, records);
        }
    }

    private static List<AttributeData> GetServiceAttributes(INamedTypeSymbol classSymbol, INamedTypeSymbol serviceAttributeSymbol)
    {
        var serviceAttributes = new List<AttributeData>();
        foreach (var attributeData in classSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, serviceAttributeSymbol))
            {
                serviceAttributes.Add(attributeData);
            }
        }

        return serviceAttributes;
    }

    /// <summary>
    /// Validates and reports diagnostics for a single <c>[Service]</c> attribute application, then
    /// (if valid) records one <see cref="RegistrationRecord"/> per resolved service type for later
    /// cross-compilation duplicate detection by <see cref="ReportDuplicates"/>.
    /// </summary>
    private static void AnalyzeAttribute(
        SymbolAnalysisContext context,
        INamedTypeSymbol classSymbol,
        AttributeData attributeData,
        string implementationTypeFqn,
        ConcurrentBag<RegistrationRecord> records)
    {
        var location = GetLocation(attributeData, classSymbol);

        // SSAL001: abstract/static classes cannot be registered. This supersedes every other
        // check for this attribute application, since the class itself is not a valid target.
        if (classSymbol.IsAbstract || classSymbol.IsStatic)
        {
            var reason = classSymbol.IsStatic ? "static" : "abstract";
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidTargetType, location, classSymbol.Name, reason));
            return;
        }

        // SSAL003: open generic classes are not supported.
        if (classSymbol.IsGenericType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenericClassNotSupported, location, classSymbol.Name));
            return;
        }

        var lifetime = AttributeArgumentReader.GetLifetime(attributeData);
        var mode = AttributeArgumentReader.GetMode(attributeData);

        // SSAL008: an out-of-range Lifetime/Mode (e.g. from `(ServiceLifetime)42`) must not be
        // silently coerced into some default by the emitter.
        if (lifetime is < (int)WellKnownLifetime.Singleton or > (int)WellKnownLifetime.Transient)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UndefinedEnumValue, location, lifetime.ToString(CultureInfo.InvariantCulture), "ServiceLifetime"));
            return;
        }

        if (mode is < (int)WellKnownRegistrationMode.Add or > (int)WellKnownRegistrationMode.Replace)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UndefinedEnumValue, location, mode.ToString(CultureInfo.InvariantCulture), "RegistrationMode"));
            return;
        }

        var keyConstant = AttributeArgumentReader.GetKeyConstant(attributeData);
        var hasKey = keyConstant is { IsNull: false };

        // SSAL005: no keyed TryAddEnumerable API exists in Microsoft.Extensions.DependencyInjection.
        if (hasKey && mode == (int)WellKnownRegistrationMode.TryAddEnumerable)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.KeyedTryAddEnumerableNotSupported, location, implementationTypeFqn));
            return;
        }

        if (!TryResolveServiceTypes(context, classSymbol, attributeData, implementationTypeFqn, location, out var serviceTypeSymbols, out var serviceTypeFqns))
        {
            return;
        }

        // SSAL007: the implementation type and every resolved service type must be accessible
        // from the generated registration code.
        if (!TypeAccessibilityChecker.IsAccessible(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InaccessibleType, location, implementationTypeFqn));
            return;
        }

        for (var i = 0; i < serviceTypeSymbols.Length; i++)
        {
            if (serviceTypeSymbols[i] is INamedTypeSymbol namedServiceType && !TypeAccessibilityChecker.IsAccessible(namedServiceType))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InaccessibleType, location, serviceTypeFqns[i]));
                return;
            }
        }

        // SSAL006: TryAddEnumerable cannot distinguish a registration whose service type is the
        // implementation type itself.
        if (mode == (int)WellKnownRegistrationMode.TryAddEnumerable && serviceTypeFqns.Contains(implementationTypeFqn, StringComparer.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SelfTryAddEnumerableNotSupported, location, implementationTypeFqn));
            return;
        }

        var keyIdentity = hasKey
            ? KeyLiteralFormatter.Format(keyConstant!.Value) ?? "<unknown>"
            : "<none>";

        foreach (var serviceTypeFqn in serviceTypeFqns)
        {
            records.Add(new RegistrationRecord(serviceTypeFqn, implementationTypeFqn, keyIdentity, location));
        }
    }

    /// <summary>
    /// Resolves the service type(s) an attribute application registers against: the explicit
    /// <c>As</c> type (reporting SSAL002 and returning <see langword="false"/> if the class does not
    /// implement/derive it), or otherwise every directly-implemented interface (or the
    /// implementation type itself, if it implements none).
    /// </summary>
    private static bool TryResolveServiceTypes(
        SymbolAnalysisContext context,
        INamedTypeSymbol classSymbol,
        AttributeData attributeData,
        string implementationTypeFqn,
        Location location,
        out ImmutableArray<ITypeSymbol> serviceTypeSymbols,
        out ImmutableArray<string> serviceTypeFqns)
    {
        var asType = AttributeArgumentReader.GetAsType(attributeData);
        if (asType is not null)
        {
            // SSAL002: the class must implement/derive the explicitly requested service type.
            if (!ServiceTypeResolver.Implements(classSymbol, asType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AsTypeNotImplemented,
                    location,
                    implementationTypeFqn,
                    asType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
                serviceTypeFqns = ImmutableArray<string>.Empty;
                return false;
            }

            serviceTypeSymbols = ImmutableArray.Create(asType);
            serviceTypeFqns = ImmutableArray.Create(asType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            return true;
        }

        var interfaces = ServiceTypeResolver.GetDirectlyImplementedInterfaces(classSymbol);
        if (interfaces.Length == 0)
        {
            serviceTypeSymbols = ImmutableArray.Create<ITypeSymbol>(classSymbol);
            serviceTypeFqns = ImmutableArray.Create(implementationTypeFqn);
        }
        else
        {
            serviceTypeSymbols = interfaces.Cast<ITypeSymbol>().ToImmutableArray();
            serviceTypeFqns = interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray();
        }

        return true;
    }

    private static void ReportDuplicates(CompilationAnalysisContext context, ConcurrentBag<RegistrationRecord> records)
    {
        if (records.IsEmpty)
        {
            return;
        }

        // Symbol actions may run concurrently, so the bag's enumeration order is not guaranteed;
        // sort deterministically before grouping so which occurrence is treated as "the first, ok"
        // one is stable across runs.
        var ordered = records
            .OrderBy(r => r.Location.SourceSpan.Start)
            .ThenBy(r => r.ServiceTypeFqn, StringComparer.Ordinal)
            .ThenBy(r => r.ImplementationTypeFqn, StringComparer.Ordinal)
            .ToList();

        foreach (var group in ordered.GroupBy(r => (r.ServiceTypeFqn, r.ImplementationTypeFqn, r.KeyIdentity)))
        {
            var items = group.ToList();
            if (items.Count < 2)
            {
                continue;
            }

            var keySuffix = group.Key.KeyIdentity == "<none>" ? string.Empty : $" with key {group.Key.KeyIdentity}";

            for (var i = 1; i < items.Count; i++)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateRegistration,
                    items[i].Location,
                    group.Key.ServiceTypeFqn,
                    group.Key.ImplementationTypeFqn,
                    keySuffix));
            }
        }
    }

    private static Location GetLocation(AttributeData attributeData, INamedTypeSymbol fallbackSymbol)
    {
        var syntaxReference = attributeData.ApplicationSyntaxReference;
        if (syntaxReference is not null)
        {
            return syntaxReference.GetSyntax().GetLocation();
        }

        return fallbackSymbol.Locations.Length > 0 ? fallbackSymbol.Locations[0] : Location.None;
    }

    private readonly record struct RegistrationRecord(string ServiceTypeFqn, string ImplementationTypeFqn, string KeyIdentity, Location Location);
}
