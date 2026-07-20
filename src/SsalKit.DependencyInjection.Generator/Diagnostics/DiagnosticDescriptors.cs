using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Diagnostics;

/// <summary>
/// Diagnostic descriptors reported by <see cref="Analysis.ServiceAttributeAnalyzer"/>.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "SsalKit.DependencyInjection";

    public static readonly DiagnosticDescriptor InvalidTargetType = new(
        id: "SSAL001",
        title: "[Service] cannot be applied to an abstract or static class",
        messageFormat: "[Service] cannot be applied to '{0}' because it is {1}; only concrete, non-static classes can be registered",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source generator can only register concrete, instantiable classes into the service collection.");

    public static readonly DiagnosticDescriptor AsTypeNotImplemented = new(
        id: "SSAL002",
        title: "The type specified by 'As' is not implemented or inherited by the decorated class",
        messageFormat: "'{0}' does not implement or inherit '{1}' specified via 'As' on [Service]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The 'As' service type must be an interface implemented by the class, or a base class/itself.");

    public static readonly DiagnosticDescriptor GenericClassNotSupported = new(
        id: "SSAL003",
        title: "[Service] cannot be applied to an open generic class",
        messageFormat: "[Service] cannot be applied to '{0}' because open generic classes are not supported",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Registering open generic services is not supported in this version of the generator.");

    public static readonly DiagnosticDescriptor DuplicateRegistration = new(
        id: "SSAL004",
        title: "Duplicate service registration",
        messageFormat: "The service type '{0}' is registered more than once for implementation '{1}'{2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The same (service type, implementation type, key) combination is registered by more than one [Service] attribute across the compilation.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor KeyedTryAddEnumerableNotSupported = new(
        id: "SSAL005",
        title: "'Key' cannot be combined with RegistrationMode.TryAddEnumerable",
        messageFormat: "'Key' cannot be combined with RegistrationMode.TryAddEnumerable on '{0}' because Microsoft.Extensions.DependencyInjection has no corresponding keyed API",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "There is no TryAddEnumerable equivalent for keyed services in Microsoft.Extensions.DependencyInjection.");
}
