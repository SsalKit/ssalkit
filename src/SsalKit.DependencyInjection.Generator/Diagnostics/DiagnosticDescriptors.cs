using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Diagnostics;

/// <summary>
/// Diagnostic descriptors reported by <see cref="Analysis.ServiceAttributeAnalyzer"/> (SSAL001-
/// SSAL015, for <c>[Service]</c>) and <see cref="Analysis.ServiceFactoryAnalyzer"/> (SSAL016-
/// SSAL020, for <c>[ServiceFactory]</c>).
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
        title: "[Service] cannot be applied to a class nested inside a generic type",
        messageFormat: "[Service] cannot be applied to '{0}' because it is nested inside a generic type; a generic class is only supported when all of its type parameters are its own",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Open generic support only covers classes whose generic context is entirely their own type parameter list. A class nested inside a generic type carries its containing type's parameters and cannot be registered.");

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

    public static readonly DiagnosticDescriptor SelfTryAddEnumerableNotSupported = new(
        id: "SSAL006",
        title: "RegistrationMode.TryAddEnumerable cannot register a type as itself",
        messageFormat: "'{0}' cannot be registered via RegistrationMode.TryAddEnumerable as its own service type because Microsoft.Extensions.DependencyInjection cannot distinguish duplicate entries; implement an interface or specify a different service type via 'As'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "TryAddEnumerable(ServiceDescriptor.Singleton<T, T>()) throws ArgumentException at runtime because Microsoft.Extensions.DependencyInjection cannot tell distinct registrations of the same implementation type apart when the service type and implementation type are identical.");

    public static readonly DiagnosticDescriptor InaccessibleType = new(
        id: "SSAL007",
        title: "[Service] type must be accessible to generated code",
        messageFormat: "'{0}' cannot be registered because it is not accessible from the generated registration code; make the type (and its containing types) at least 'internal' and not file-local",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated registration extension method lives in a separate file, in the Microsoft.Extensions.DependencyInjection namespace, in the same assembly; it can only reference types -- including a 'typeof(...)' Key value and any generic type arguments -- that are (along with their containing types) at least internal and not file-local.");

    public static readonly DiagnosticDescriptor UndefinedEnumValue = new(
        id: "SSAL008",
        title: "Undefined enum value on [Service]",
        messageFormat: "The value '{0}' is not a defined '{1}' value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Lifetime and Mode arguments of [Service] must be one of the values defined by ServiceLifetime/RegistrationMode; an out-of-range value (e.g. from an explicit numeric cast) is silently mishandled otherwise.");

    public static readonly DiagnosticDescriptor OpenGenericServiceTypeNotExactMatch = new(
        id: "SSAL009",
        title: "Open generic service type must use the class's own type parameters",
        messageFormat: "'{0}' cannot be registered as '{1}' because an open generic class can only be registered as itself or as an implemented interface or base class whose type arguments are exactly the class's own type parameters in declaration order",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Microsoft.Extensions.DependencyInjection resolves an open generic registration by substituting the requested service type's arguments positionally into the implementation type. Any other shape (a closed or non-generic service type, reordered, partially used, or nested type arguments) either cannot be constructed or produces a type that does not implement the requested service, so it is rejected at compile time.");

    public static readonly DiagnosticDescriptor OpenGenericInstanceNotShared = new(
        id: "SSAL010",
        title: "Open generic registrations do not share an instance across service types",
        messageFormat: "'{0}' is registered as {1} service types as an open generic; each closed service type will resolve to a separate instance because open generic registrations cannot use forwarding factories",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "For a non-generic class, a Singleton/Scoped registration against multiple service types shares one instance via forwarding factories. Open generic registrations cannot use factories, so every service type gets an independent registration and instances are not shared. Suppress this warning if separate instances are intended.");

    public static readonly DiagnosticDescriptor FactoryMethodNotFound = new(
        id: "SSAL011",
        title: "'Factory' method not found",
        messageFormat: "No ordinary method named '{0}' is declared on '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The 'Factory' argument of [Service] must name an ordinary (non-property, non-operator) method declared directly on the decorated class. This also covers an empty-string 'Factory' value, which never matches any method.");

    public static readonly DiagnosticDescriptor FactoryMethodInvalid = new(
        id: "SSAL012",
        title: "'Factory' method has an unusable signature",
        messageFormat: "One or more methods named '{0}' are declared on '{1}', but none has a usable signature: a factory method must be static, non-generic, have no parameters or a single 'System.IServiceProvider' parameter, and return exactly '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A method named by 'Factory' was found, but no overload of it is static, non-generic, parameterless-or-single-IServiceProvider-parameter, and returns exactly the decorated class.");

    public static readonly DiagnosticDescriptor FactoryOnOpenGenericNotSupported = new(
        id: "SSAL013",
        title: "'Factory' cannot be used on an open generic class",
        messageFormat: "'Factory' cannot be used on open generic class '{0}' because Microsoft.Extensions.DependencyInjection has no factory-based registration for open generics",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Microsoft.Extensions.DependencyInjection's open generic registration overloads (Type-based, not <TService, TImplementation>) have no factory-delegate counterpart, so an open generic class cannot combine [Service] with a 'Factory'.");

    public static readonly DiagnosticDescriptor FactoryMethodInaccessible = new(
        id: "SSAL014",
        title: "'Factory' method is not accessible to generated code",
        messageFormat: "The factory method '{0}.{1}' is not accessible from the generated registration code; it must be at least 'internal'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The chosen factory method is invoked from the generated registration extension method, which lives in a separate file in the same assembly; a private or protected factory method cannot be called from there.");

    public static readonly DiagnosticDescriptor ConflictingImplementations = new(
        id: "SSAL015",
        title: "Multiple implementations registered for the same service type",
        messageFormat: "The service type '{0}'{1} is registered with {2} different implementation types ({3}); a single-instance resolution returns whichever of them is registered last, and the generator emits registrations ordered by implementation type name rather than by source order",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Microsoft.Extensions.DependencyInjection resolves a single service instance from the last matching registration, and this generator emits registrations sorted by implementation type name, so which implementation wins is decided by type naming rather than by the order the [Service] attributes appear in source -- renaming a class can silently change which one is resolved. If several implementations are meant to be injected together as IEnumerable<T>, register every one of them with RegistrationMode.TryAddEnumerable (a group consisting only of TryAddEnumerable registrations is never reported); otherwise disambiguate them with distinct 'Key' values, or suppress this warning if one deliberately overrides the other.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor ServiceFactoryTargetNotInterface = new(
        id: "SSAL016",
        title: "[ServiceFactory] can only be applied to an interface",
        messageFormat: "[ServiceFactory] cannot be applied to '{0}' because it is {1}; only an interface can declare a service factory",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator implements a [ServiceFactory] target by emitting a class that implements it, which is only possible for an interface. [AttributeUsage(AttributeTargets.Interface)] already makes any other target a CS0592 compiler error, so this rule only ever fires alongside that error, as a defence against the attribute's usage being widened without the generator following suit.");

    public static readonly DiagnosticDescriptor ServiceFactoryMemberShapeInvalid = new(
        id: "SSAL017",
        title: "[ServiceFactory] interface must declare exactly one method",
        messageFormat: "[ServiceFactory] interface '{0}' must declare exactly one member and that member must be an ordinary, non-static method, but {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated implementation only knows how to implement a single enum-keyed lookup method. Any additional member -- another method, a property, an event, a field, or a nested type -- would be left unimplemented, so the whole interface is rejected rather than generating code that does not compile.");

    public static readonly DiagnosticDescriptor ServiceFactoryMethodSignatureInvalid = new(
        id: "SSAL018",
        title: "[ServiceFactory] method must take a single enum parameter and return a service type",
        messageFormat: "The method '{0}.{1}' cannot be used as a service factory because {2}; it must be non-generic, take exactly one by-value parameter of an enum type, and return a non-void service type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated implementation forwards its single parameter verbatim as the service key to GetRequiredKeyedService<TReturn>, which requires exactly one by-value enum parameter and a non-void, non-by-ref return type. A generic method has no single closed form to implement against.");

    public static readonly DiagnosticDescriptor ServiceFactoryGenericNotSupported = new(
        id: "SSAL019",
        title: "[ServiceFactory] cannot be applied to a generic interface or one nested inside a generic type",
        messageFormat: "[ServiceFactory] cannot be applied to '{0}' because it is {1}; a service factory interface must be non-generic and not nested inside a generic type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated implementation is a non-generic class registered as a singleton against one closed service type. An open generic factory interface -- or a non-generic one that inherits type parameters from a containing generic type -- has no single closed form to register, so it is rejected at compile time.");

    public static readonly DiagnosticDescriptor ServiceFactoryInaccessibleType = new(
        id: "SSAL020",
        title: "[ServiceFactory] type must be accessible to generated code",
        messageFormat: "'{0}' cannot be used by the service factory generated for '{1}' because it is not accessible from the generated code; make the type (and its containing types) at least 'internal' and not file-local",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated factory implementation lives in a separate file, in the SsalKit.DependencyInjection.Generated namespace, in the same assembly; it can only name the factory interface, its method's enum parameter type, and its return type when each of those (along with their containing types) is at least internal and not file-local.");
}
