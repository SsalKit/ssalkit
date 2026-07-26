using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SsalKit.DependencyInjection.Generator.Diagnostics;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.DependencyInjection.Generator.Parsing;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Analysis;

/// <summary>
/// Reports diagnostics SSAL001-SSAL015 for invalid or conflicting uses of
/// <c>[SsalKit.DependencyInjection.Service]</c>, plus the two rules that span <c>[Service]</c> and
/// the convention scan: SSAL027 (both bind the same service type) and SSAL028 (a registered class
/// has no public constructor).
/// </summary>
/// <remarks>
/// SSAL027 and SSAL028 live here rather than in <see cref="RegisterImplementationsOfAnalyzer"/>
/// because both are questions about a <em>registration</em>, and this is the analyzer that already
/// knows what a <c>[Service]</c> registers: which service types it resolves to, under which key, at
/// which location. The convention half of each rule needs only the match set, which comes from the
/// same <see cref="ConventionImplementationMatcher"/> the scanner and the other analyzer use, so the
/// three still cannot disagree about what a contract matched. The alternative -- splitting one rule
/// across two analyzers -- would have meant lifting <c>[Service]</c>'s whole service-type resolution
/// into shared code for a single caller.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string ServiceAttributeMetadataName = "SsalKit.DependencyInjection.ServiceAttribute";

    /// <summary>
    /// The <see cref="RegistrationRecord.KeyIdentity"/> placeholder for a non-keyed registration.
    /// Not a possible <c>KeyLiteralFormatter</c>/<see cref="KeyIdentityNormalizer"/> output, so it
    /// can never collide with a real key.
    /// </summary>
    private const string NoKeyIdentity = "<none>";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.InvalidTargetType,
        DiagnosticDescriptors.AsTypeNotImplemented,
        DiagnosticDescriptors.GenericClassNotSupported,
        DiagnosticDescriptors.DuplicateRegistration,
        DiagnosticDescriptors.KeyedTryAddEnumerableNotSupported,
        DiagnosticDescriptors.SelfTryAddEnumerableNotSupported,
        DiagnosticDescriptors.InaccessibleType,
        DiagnosticDescriptors.UndefinedEnumValue,
        DiagnosticDescriptors.OpenGenericServiceTypeNotExactMatch,
        DiagnosticDescriptors.OpenGenericInstanceNotShared,
        DiagnosticDescriptors.FactoryMethodNotFound,
        DiagnosticDescriptors.FactoryMethodInvalid,
        DiagnosticDescriptors.FactoryOnOpenGenericNotSupported,
        DiagnosticDescriptors.FactoryMethodInaccessible,
        DiagnosticDescriptors.ConflictingImplementations,
        DiagnosticDescriptors.ServiceAndConventionOverlap,
        DiagnosticDescriptors.NoPublicConstructor);

    public override void Initialize(AnalysisContext context)
    {
        // Generated code is analyzed and reported on: a [Service] emitted by another generator
        // produces exactly the same registration a hand-written one does, so every rule from SSAL001
        // to SSAL015 applies to it, and leaving it out of the SSAL004/SSAL015 tallies made those two
        // report on an incomplete picture of the compilation. This generator's own output is
        // excluded by name instead (see GeneratedOutputRecognizer).
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
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

            // Only read when the compilation declares at least one usable contract, which is the
            // fast path for every assembly that does not use the feature: with no declarations there
            // is no convention block to collide with, so SSAL027 has nothing to say and the
            // per-symbol matching below is skipped entirely.
            var contracts = ReadValidContracts(compilationStartContext.Compilation);
            var conventionRecords = contracts.IsEmpty ? null : new ConcurrentBag<ConventionRecord>();

            compilationStartContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext, serviceAttributeSymbol, records, contracts, conventionRecords),
                SymbolKind.NamedType);

            compilationStartContext.RegisterCompilationEndAction(
                endContext => ReportCrossRegistrationDiagnostics(endContext, records, conventionRecords));
        });
    }

    /// <summary>
    /// Every usable <c>[assembly: RegisterImplementationsOf]</c> declaration in the compilation, via
    /// the same <see cref="ContractDeclarationReader"/> the scanner and
    /// <see cref="RegisterImplementationsOfAnalyzer"/> use. Rejected declarations are dropped without
    /// a word here -- reporting them is the other analyzer's job, and this one must not double it.
    /// </summary>
    private static ImmutableArray<ContractDeclaration> ReadValidContracts(Compilation compilation)
    {
        var declarations = ContractDeclarationReader.Read(compilation);
        if (declarations.IsEmpty)
        {
            return ImmutableArray<ContractDeclaration>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ContractDeclaration>();
        foreach (var declaration in declarations)
        {
            if (declaration.Kind == ContractValidationKind.Valid)
            {
                builder.Add(declaration);
            }
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol serviceAttributeSymbol,
        ConcurrentBag<RegistrationRecord> records,
        ImmutableArray<ContractDeclaration> contracts,
        ConcurrentBag<ConventionRecord>? conventionRecords)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } classSymbol)
        {
            return;
        }

        // This generator's own factory implementations are not consumer registrations, and the
        // generator itself never sees them; see GeneratedOutputRecognizer.
        if (GeneratedOutputRecognizer.IsGeneratorOutput(classSymbol))
        {
            return;
        }

        var serviceAttributes = GetServiceAttributes(classSymbol, serviceAttributeSymbol);
        if (serviceAttributes.Count == 0)
        {
            AnalyzeConventionCandidate(context, classSymbol, serviceAttributeSymbol, contracts, conventionRecords);
            return;
        }

        var implementationTypeFqn = SymbolFacts.ToFqn(classSymbol);

        foreach (var attributeData in serviceAttributes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            AnalyzeAttribute(context, classSymbol, attributeData, implementationTypeFqn, records);
        }
    }

    /// <summary>
    /// Records what the convention scan registers for a class that carries no <c>[Service]</c> of
    /// its own, for SSAL027 to compare against at compilation end, and reports SSAL028 for it.
    /// </summary>
    /// <remarks>
    /// A class carrying <c>[Service]</c> never reaches here, because it is excluded from every scan
    /// (<see cref="ConventionImplementationMatcher.IsCandidate"/>) -- which is exactly why SSAL027 is
    /// a cross-<em>class</em> rule: the explicit and the convention registration always come from two
    /// different classes binding the same service type.
    /// </remarks>
    private static void AnalyzeConventionCandidate(
        SymbolAnalysisContext context,
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol serviceAttributeSymbol,
        ImmutableArray<ContractDeclaration> contracts,
        ConcurrentBag<ConventionRecord>? conventionRecords)
    {
        if (conventionRecords is null
            || !ConventionImplementationMatcher.IsCandidate(classSymbol, serviceAttributeSymbol, context.Compilation))
        {
            return;
        }

        var matched = false;

        foreach (var contract in contracts)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (var match in ConventionImplementationMatcher.Match(classSymbol, contract, context.Compilation))
            {
                matched = true;
                conventionRecords.Add(new ConventionRecord(match.ServiceTypeFqn, contract.ContractFqn, contract.Mode));
            }
        }

        // SSAL028: reported once for the class rather than once per contract that matched it -- the
        // missing constructor is a property of the class, and every contract would say the same
        // thing about it.
        if (matched && HasNoPublicConstructor(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoPublicConstructor,
                GetDeclarationLocation(classSymbol),
                SymbolFacts.ToFqn(classSymbol)));
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when Microsoft.Extensions.DependencyInjection could not
    /// activate <paramref name="classSymbol"/>, i.e. when none of its instance constructors is
    /// <see langword="public"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The container's constructor selection enumerates public constructors only, so this is a
    /// decidable, exact rule rather than a heuristic -- including for an open generic class, whose
    /// closed instantiations inherit the accessibility of the constructors declared on the open
    /// definition, so no carve-out is warranted there either.
    /// </para>
    /// <para>
    /// The two cases the rule deliberately stays silent about are the ones where it cannot see the
    /// whole picture: a registration that names a <c>Factory</c> (the generated code calls the method
    /// and never a constructor, so constructor accessibility is irrelevant -- checked by the caller),
    /// and a type reporting no instance constructor at all, which no source-declared class does and
    /// which therefore signals a symbol this analyzer should not be drawing conclusions from.
    /// </para>
    /// </remarks>
    private static bool HasNoPublicConstructor(INamedTypeSymbol classSymbol)
    {
        var constructors = classSymbol.InstanceConstructors;
        if (constructors.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var constructor in constructors)
        {
            if (constructor.DeclaredAccessibility == Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The class's own declaration site, for a diagnostic that has no attribute to point at. The
    /// first <em>source</em> location, so a partial class is reported once, at its first part.
    /// </summary>
    private static Location GetDeclarationLocation(INamedTypeSymbol classSymbol)
    {
        foreach (var location in classSymbol.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return Location.None;
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
    /// cross-compilation conflict detection by <see cref="ReportCrossRegistrationDiagnostics"/>.
    /// </summary>
    private static void AnalyzeAttribute(
        SymbolAnalysisContext context,
        INamedTypeSymbol classSymbol,
        AttributeData attributeData,
        string implementationTypeFqn,
        ConcurrentBag<RegistrationRecord> records)
    {
        var location = AttributeLocations.GetLocation(attributeData, classSymbol);

        // SSAL001: abstract/static classes cannot be registered. This supersedes every other
        // check for this attribute application, since the class itself is not a valid target.
        if (classSymbol.IsAbstract || classSymbol.IsStatic)
        {
            var reason = classSymbol.IsStatic ? "static" : "abstract";
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidTargetType, location, classSymbol.Name, reason));
            return;
        }

        // SSAL003: a class nested inside a generic type carries its containing type's type
        // parameters and can never be registered as an open generic, regardless of its own arity.
        if (ServiceTypeResolver.IsNestedInGenericType(classSymbol))
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

        // SSAL011/SSAL012/SSAL013/SSAL014: resolve an explicit 'Factory'. Independent of Key/Mode/
        // As, so it is checked here, before service-type resolution, in lockstep with
        // ServiceAttributeParser.
        var factoryName = AttributeArgumentReader.GetFactoryName(attributeData);
        if (factoryName is not null && !TryReportFactoryDiagnostic(context, classSymbol, factoryName, location, implementationTypeFqn))
        {
            return;
        }

        if (!TryResolveServiceTypes(context, classSymbol, attributeData, implementationTypeFqn, location, out var serviceTypeSymbols, out var serviceTypeFqns))
        {
            return;
        }

        // SSAL007: the implementation type and every resolved service type must be accessible
        // from the generated registration code.
        if (!TypeAccessibilityChecker.IsAccessible(classSymbol, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InaccessibleType, location, implementationTypeFqn));
            return;
        }

        for (var i = 0; i < serviceTypeSymbols.Length; i++)
        {
            if (!TypeAccessibilityChecker.IsAccessible(serviceTypeSymbols[i], context.Compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InaccessibleType, location, serviceTypeFqns[i]));
                return;
            }
        }

        // SSAL007: a `typeof(...)` Key value must be accessible too, since it is emitted verbatim
        // into the same generated code as the implementation/service types.
        if (keyConstant is { IsNull: false, Kind: TypedConstantKind.Type } typedKeyConstant
            && typedKeyConstant.Value is ITypeSymbol keyTypeSymbol
            && !TypeAccessibilityChecker.IsAccessible(keyTypeSymbol, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InaccessibleType,
                location,
                SymbolFacts.ToFqn(keyTypeSymbol)));
            return;
        }

        // SSAL006: TryAddEnumerable cannot distinguish a registration whose service type is the
        // implementation type itself. This is a symbol-based check, not an FQN string comparison:
        // for an open generic class with an explicit `As = typeof(C<>)` (self, via an unbound
        // generic reference), the service type's *display* FQN ("global::Ns.C<>") never string-
        // matches the implementation's display FQN ("global::Ns.C<T>"), even though they denote
        // the same class -- see ServiceTypeResolver.IsSelfServiceType.
        if (mode == (int)WellKnownRegistrationMode.TryAddEnumerable
            && serviceTypeSymbols.Any(serviceTypeSymbol => ServiceTypeResolver.IsSelfServiceType(classSymbol, serviceTypeSymbol)))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SelfTryAddEnumerableNotSupported, location, implementationTypeFqn));
            return;
        }

        // SSAL010: an open generic Singleton/Scoped registration cannot share one instance across
        // 2+ service types the way a non-generic class does, because Microsoft.Extensions.
        // DependencyInjection has no forwarding-factory mechanism for open generics. This is a
        // warning, not an error -- the generator still emits every registration -- so execution
        // falls through to recording below rather than returning.
        if (classSymbol.Arity > 0
            && serviceTypeFqns.Length >= 2
            && mode != (int)WellKnownRegistrationMode.TryAddEnumerable
            && lifetime is (int)WellKnownLifetime.Singleton or (int)WellKnownLifetime.Scoped)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.OpenGenericInstanceNotShared,
                location,
                implementationTypeFqn,
                serviceTypeFqns.Length.ToString(CultureInfo.InvariantCulture)));
        }

        // SSAL028: with no 'Factory' to call, the generated registration hands the class to the
        // container's own constructor-based activation, which only considers public constructors.
        // A warning like SSAL010, so execution falls through to recording rather than returning.
        if (factoryName is null && HasNoPublicConstructor(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoPublicConstructor, location, implementationTypeFqn));
        }

        var keyIdentity = hasKey
            ? GetKeyIdentity(keyConstant!.Value, context.Compilation)
            : NoKeyIdentity;

        // SSAL004/SSAL015 conflict detection must key an open generic registration on its typeof-form
        // identity (e.g. "global::Ns.IRepo<>"), not the ordinary display FQN used for the
        // messages above (e.g. "global::Ns.IRepo<T>") -- otherwise `[Service]` (which infers
        // IRepo<T> from the implemented interface) and `[Service(As = typeof(IRepo<>))]` on the
        // same class would never be recognized as registering the exact same open generic service.
        var recordImplementationTypeFqn = classSymbol.Arity > 0
            ? OpenGenericTypeofFormatter.Format(classSymbol)
            : implementationTypeFqn;

        for (var i = 0; i < serviceTypeFqns.Length; i++)
        {
            var recordServiceTypeFqn = classSymbol.Arity > 0 && serviceTypeSymbols[i] is INamedTypeSymbol namedServiceType
                ? OpenGenericTypeofFormatter.Format(namedServiceType)
                : serviceTypeFqns[i];

            records.Add(new RegistrationRecord(recordServiceTypeFqn, recordImplementationTypeFqn, keyIdentity, mode, location));
        }
    }

    /// <summary>
    /// Resolves an explicit <c>Factory</c> named argument via the shared
    /// <see cref="FactoryMethodResolver"/> and reports the corresponding diagnostic (SSAL011-
    /// SSAL014) if resolution did not succeed. Mirrors
    /// <c>ServiceAttributeParser.TryResolveFactory</c>'s validation exactly.
    /// </summary>
    /// <returns><see langword="true"/> if resolution succeeded (nothing reported); otherwise <see langword="false"/>.</returns>
    private static bool TryReportFactoryDiagnostic(
        SymbolAnalysisContext context,
        INamedTypeSymbol classSymbol,
        string factoryName,
        Location location,
        string implementationTypeFqn)
    {
        var resolution = FactoryMethodResolver.Resolve(classSymbol, factoryName, classSymbol.Arity > 0);

        switch (resolution.Kind)
        {
            case FactoryResolutionKind.Success:
                return true;

            case FactoryResolutionKind.OpenGenericNotSupported:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.FactoryOnOpenGenericNotSupported, location, implementationTypeFqn));
                return false;

            case FactoryResolutionKind.NotFound:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.FactoryMethodNotFound, location, factoryName, implementationTypeFqn));
                return false;

            case FactoryResolutionKind.Invalid:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.FactoryMethodInvalid, location, factoryName, implementationTypeFqn));
                return false;

            case FactoryResolutionKind.Inaccessible:
            default:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.FactoryMethodInaccessible, location, implementationTypeFqn, factoryName));
                return false;
        }
    }

    /// <summary>
    /// Computes the identity string used to group registrations by key for SSAL004 duplicate
    /// detection. For a <c>typeof(...)</c> Key, this is a *runtime-identity*-normalized form (see
    /// <see cref="KeyIdentityNormalizer"/>) rather than the source-level spelling
    /// <see cref="KeyLiteralFormatter"/> produces for the generated code, so that e.g.
    /// <c>typeof((int A, string B))</c> and <c>typeof((int, string))</c> -- the exact same runtime
    /// <see cref="System.Type"/> -- are correctly treated as the same key. Every other kind of key
    /// (string/int/enum/... constants) has no such source-vs-runtime distinction, so
    /// <see cref="KeyLiteralFormatter.Format"/>'s output is already a correct identity for them.
    /// </summary>
    private static string GetKeyIdentity(TypedConstant keyConstant, Compilation compilation)
    {
        if (keyConstant.Kind == TypedConstantKind.Type && keyConstant.Value is ITypeSymbol keyTypeSymbol)
        {
            return KeyIdentityNormalizer.GetNormalizedIdentity(keyTypeSymbol, compilation);
        }

        return KeyLiteralFormatter.Format(keyConstant) ?? "<unknown>";
    }

    /// <summary>
    /// Resolves the service type(s) an attribute application registers against: the explicit
    /// <c>As</c> type (reporting SSAL002 and returning <see langword="false"/> if the class does not
    /// implement/derive it), or otherwise every directly-implemented interface (or the
    /// implementation type itself, if it implements none). For an open generic class (see
    /// <see cref="ServiceTypeResolver.IsNestedInGenericType"/>), every candidate service type must
    /// additionally satisfy the exact-match rule (SSAL009) -- see
    /// <see cref="TryResolveOpenGenericAsType"/> for the <c>As</c> case.
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
        var isOpenGeneric = classSymbol.Arity > 0;
        var asType = AttributeArgumentReader.GetAsType(attributeData);
        if (asType is not null)
        {
            if (isOpenGeneric)
            {
                return TryResolveOpenGenericAsType(
                    context, classSymbol, asType, implementationTypeFqn, location, out serviceTypeSymbols, out serviceTypeFqns);
            }

            // SSAL002: the class must implement/derive the explicitly requested service type.
            if (!ServiceTypeResolver.Implements(classSymbol, asType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AsTypeNotImplemented,
                    location,
                    implementationTypeFqn,
                    SymbolFacts.ToFqn(asType)));
                serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
                serviceTypeFqns = ImmutableArray<string>.Empty;
                return false;
            }

            serviceTypeSymbols = ImmutableArray.Create(asType);
            serviceTypeFqns = ImmutableArray.Create(SymbolFacts.ToFqn(asType));
            return true;
        }

        var interfaces = ServiceTypeResolver.GetDirectlyImplementedInterfaces(classSymbol);
        if (interfaces.Length == 0)
        {
            serviceTypeSymbols = ImmutableArray.Create<ITypeSymbol>(classSymbol);
            serviceTypeFqns = ImmutableArray.Create(implementationTypeFqn);
            return true;
        }

        if (isOpenGeneric)
        {
            // SSAL009: every directly-implemented interface must be an exact-match open generic
            // service type when there is no explicit `As` to redirect to a single one -- the whole
            // attribute application is invalid (no partial/silent skipping) if any one of them
            // isn't; the escape hatch is an explicit `As`.
            foreach (var iface in interfaces)
            {
                if (!ServiceTypeResolver.IsExactMatchOpenGenericServiceType(classSymbol, iface))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.OpenGenericServiceTypeNotExactMatch,
                        location,
                        implementationTypeFqn,
                        SymbolFacts.ToFqn(iface)));
                    serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
                    serviceTypeFqns = ImmutableArray<string>.Empty;
                    return false;
                }
            }
        }

        serviceTypeSymbols = interfaces.Cast<ITypeSymbol>().ToImmutableArray();
        serviceTypeFqns = interfaces.Select(i => SymbolFacts.ToFqn(i)).ToImmutableArray();
        return true;
    }

    /// <summary>
    /// Resolves an explicit <c>As = typeof(X&lt;&gt;)</c> service type applied to an open generic
    /// class: reports SSAL009 immediately for a closed/non-generic <c>As</c> value (never valid for
    /// an open generic implementation), SSAL002 if the class implements/derives no instantiation of
    /// <c>X</c> at all, or SSAL009 if it does but the instantiation isn't an exact-match shape.
    /// </summary>
    private static bool TryResolveOpenGenericAsType(
        SymbolAnalysisContext context,
        INamedTypeSymbol classSymbol,
        ITypeSymbol asType,
        string implementationTypeFqn,
        Location location,
        out ImmutableArray<ITypeSymbol> serviceTypeSymbols,
        out ImmutableArray<string> serviceTypeFqns)
    {
        serviceTypeSymbols = ImmutableArray<ITypeSymbol>.Empty;
        serviceTypeFqns = ImmutableArray<string>.Empty;

        if (asType is not INamedTypeSymbol { IsUnboundGenericType: true } unboundAsType)
        {
            // SSAL009: a closed/non-generic As service type can never be valid for an open generic
            // implementation type -- Microsoft.Extensions.DependencyInjection requires the service
            // type to be open too, with a matching arity, to substitute a resolved closed service
            // type's arguments positionally.
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.OpenGenericServiceTypeNotExactMatch,
                location,
                implementationTypeFqn,
                SymbolFacts.ToFqn(asType)));
            return false;
        }

        var instantiation = ServiceTypeResolver.FindOpenGenericAsInstantiation(classSymbol, unboundAsType);
        if (instantiation is null)
        {
            // SSAL002: not implemented/derived at all.
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AsTypeNotImplemented,
                location,
                implementationTypeFqn,
                SymbolFacts.ToFqn(unboundAsType)));
            return false;
        }

        if (!ServiceTypeResolver.IsExactMatchOpenGenericServiceType(classSymbol, instantiation))
        {
            // SSAL009: implemented/derived, but not in the required exact-match shape.
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.OpenGenericServiceTypeNotExactMatch,
                location,
                implementationTypeFqn,
                SymbolFacts.ToFqn(instantiation)));
            return false;
        }

        serviceTypeSymbols = ImmutableArray.Create<ITypeSymbol>(unboundAsType);
        serviceTypeFqns = ImmutableArray.Create(SymbolFacts.ToFqn(unboundAsType));
        return true;
    }

    /// <summary>
    /// Runs the compilation-wide (CompilationEnd) checks that can only be decided once every
    /// <c>[Service]</c> application in the compilation has been seen: SSAL004 (the exact same
    /// (service type, implementation type, key) triple registered more than once), SSAL015
    /// (one (service type, key) pair registered with two or more *different* implementation types),
    /// and SSAL027 (a service type bound by both a <c>[Service]</c> and a convention scan).
    /// </summary>
    private static void ReportCrossRegistrationDiagnostics(
        CompilationAnalysisContext context,
        ConcurrentBag<RegistrationRecord> records,
        ConcurrentBag<ConventionRecord>? conventionRecords)
    {
        if (records.IsEmpty)
        {
            return;
        }

        // Symbol actions may run concurrently, so the bag's enumeration order is not guaranteed;
        // sort deterministically before grouping so which occurrence is treated as "the first, ok"
        // one (SSAL004) and the order diagnostics are reported in are stable across runs. The file
        // path is part of the sort because a source span offset alone is not unique across a
        // multi-file compilation: two attributes at the same offset in two different files would
        // otherwise be ordered by whichever symbol action happened to finish first.
        var ordered = records
            .OrderBy(r => r.Location.SourceSpan.Start)
            .ThenBy(r => r.Location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(r => r.ServiceTypeFqn, StringComparer.Ordinal)
            .ThenBy(r => r.ImplementationTypeFqn, StringComparer.Ordinal)
            .ToList();

        ReportDuplicates(context, ordered);
        ReportConflictingImplementations(context, ordered);
        ReportServiceConventionOverlaps(context, ordered, conventionRecords);
    }

    /// <summary>
    /// SSAL027: a service type that a <c>[Service]</c> binds and that a convention scan binds too,
    /// through some other class, under a mode that competes for the same resolution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TryAddEnumerable</c> contracts are exempt: they are additive by construction (the mode
    /// exists so that several implementations of one service type coexist as
    /// <c>IEnumerable&lt;T&gt;</c>), and they are the default for this attribute, so the rule stays
    /// quiet for the shape the feature is designed around. The other three modes each end up
    /// deciding a single-instance resolution -- <c>Add</c> and <c>TryAdd</c> by being emitted last,
    /// <c>Replace</c> by deleting the <c>[Service]</c> registration outright -- purely because
    /// <c>ServiceRegistrationEmitter</c> writes the convention block after the <c>[Service]</c> one.
    /// </para>
    /// <para>
    /// Only non-keyed <c>[Service]</c> records can collide, because a convention registration never
    /// carries a key: a keyed explicit registration and a non-keyed convention one are resolved
    /// through different lookups and never shadow one another.
    /// </para>
    /// </remarks>
    private static void ReportServiceConventionOverlaps(
        CompilationAnalysisContext context,
        List<RegistrationRecord> ordered,
        ConcurrentBag<ConventionRecord>? conventionRecords)
    {
        if (conventionRecords is null || conventionRecords.IsEmpty)
        {
            return;
        }

        var contractsByServiceType = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var record in conventionRecords)
        {
            if (record.Mode == (int)WellKnownRegistrationMode.TryAddEnumerable)
            {
                continue;
            }

            if (!contractsByServiceType.TryGetValue(record.ServiceTypeFqn, out var contracts))
            {
                contracts = new SortedSet<string>(StringComparer.Ordinal);
                contractsByServiceType.Add(record.ServiceTypeFqn, contracts);
            }

            contracts.Add(record.ContractFqn);
        }

        if (contractsByServiceType.Count == 0)
        {
            return;
        }

        foreach (var record in ordered)
        {
            if (record.KeyIdentity != NoKeyIdentity
                || !contractsByServiceType.TryGetValue(record.ServiceTypeFqn, out var contracts))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ServiceAndConventionOverlap,
                record.Location,
                record.ServiceTypeFqn,
                string.Join(", ", contracts.Select(contract => $"'{contract}'"))));
        }
    }

    private static void ReportDuplicates(CompilationAnalysisContext context, List<RegistrationRecord> ordered)
    {
        foreach (var group in ordered.GroupBy(r => (r.ServiceTypeFqn, r.ImplementationTypeFqn, r.KeyIdentity)))
        {
            var items = group.ToList();
            if (items.Count < 2)
            {
                continue;
            }

            var keySuffix = FormatKeySuffix(group.Key.KeyIdentity);

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

    /// <summary>
    /// SSAL015: reports every (service type, key) pair that ends up bound to two or more
    /// <em>different</em> implementation types, unless every registration in the pair uses
    /// <see cref="WellKnownRegistrationMode.TryAddEnumerable"/> (for which multiple implementations
    /// are the whole point, since they are consumed together as <c>IEnumerable&lt;T&gt;</c>).
    /// </summary>
    /// <remarks>
    /// This is deliberately a different grouping key than SSAL004's: SSAL004 groups on the full
    /// (service type, implementation type, key) triple and therefore only ever fires for a
    /// genuinely repeated registration, which leaves the far more common "IFoo is bound to both
    /// FooA and FooB" mistake unreported. It matters more here than in a hand-written
    /// <c>ConfigureServices</c> because <c>ServiceRegistrationEmitter</c> sorts the emitted
    /// registrations by implementation type FQN, so combined with Microsoft.Extensions.
    /// DependencyInjection's last-registration-wins rule for a single-instance resolution, the
    /// winner is decided by type *naming* -- not by the order the attributes appear in source.
    /// <para>
    /// Every registration in the conflicting group is reported (rather than all-but-the-first, as
    /// SSAL004 does): there is no "first one is fine" registration here -- each one is equally
    /// responsible for the ambiguity, and each is an equally valid place to fix it.
    /// </para>
    /// </remarks>
    private static void ReportConflictingImplementations(CompilationAnalysisContext context, List<RegistrationRecord> ordered)
    {
        foreach (var group in ordered.GroupBy(r => (r.ServiceTypeFqn, r.KeyIdentity)))
        {
            var items = group.ToList();

            // Ordinal-sorted, which is exactly the order ServiceRegistrationEmitter emits them in,
            // so the last name listed in the message is the one that actually wins.
            var implementationTypeFqns = items
                .Select(r => r.ImplementationTypeFqn)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(fqn => fqn, StringComparer.Ordinal)
                .ToList();

            // A single implementation type registered any number of times is SSAL004's business,
            // not this diagnostic's.
            if (implementationTypeFqns.Count < 2)
            {
                continue;
            }

            // A group made up purely of TryAddEnumerable registrations is the intended way to bind
            // several implementations to one service type; nothing is shadowed, so stay silent.
            if (items.TrueForAll(r => r.Mode == (int)WellKnownRegistrationMode.TryAddEnumerable))
            {
                continue;
            }

            var keySuffix = FormatKeySuffix(group.Key.KeyIdentity);
            var implementationList = string.Join(", ", implementationTypeFqns);

            foreach (var item in items)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConflictingImplementations,
                    item.Location,
                    group.Key.ServiceTypeFqn,
                    keySuffix,
                    implementationTypeFqns.Count.ToString(CultureInfo.InvariantCulture),
                    implementationList));
            }
        }
    }

    private static string FormatKeySuffix(string keyIdentity) =>
        keyIdentity == NoKeyIdentity ? string.Empty : $" with key {keyIdentity}";

    private readonly record struct RegistrationRecord(
        string ServiceTypeFqn,
        string ImplementationTypeFqn,
        string KeyIdentity,
        int Mode,
        Location Location);

    /// <summary>
    /// One registration a convention scan produces, reduced to what SSAL027 compares against: never
    /// keyed, and identified by the contract that produced it so the message can name it.
    /// </summary>
    private readonly record struct ConventionRecord(string ServiceTypeFqn, string ContractFqn, int Mode);
}
