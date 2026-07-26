using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Diagnostics;

/// <summary>
/// Diagnostic descriptors reported by <see cref="Analysis.ServiceAttributeAnalyzer"/> (SSAL001-
/// SSAL015, for <c>[Service]</c>), <see cref="Analysis.ServiceFactoryAnalyzer"/> (SSAL016-SSAL020,
/// for <c>[ServiceFactory]</c>), and <see cref="Analysis.RegisterImplementationsOfAnalyzer"/>
/// (SSAL021-SSAL026, for <c>[assembly: RegisterImplementationsOf]</c>).
/// </summary>
internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSAL", "SsalKit.DependencyInjection");

    public static readonly DiagnosticDescriptor InvalidTargetType = Factory.Error(
        1,
        "[Service] cannot be applied to an abstract or static class",
        "[Service] cannot be applied to '{0}' because it is {1}; only concrete, non-static classes can be registered",
        "The source generator can only register concrete, instantiable classes into the service collection.");

    public static readonly DiagnosticDescriptor AsTypeNotImplemented = Factory.Error(
        2,
        "The type specified by 'As' is not implemented or inherited by the decorated class",
        "'{0}' does not implement or inherit '{1}' specified via 'As' on [Service]",
        "The 'As' service type must be an interface implemented by the class, or a base class/itself.");

    public static readonly DiagnosticDescriptor GenericClassNotSupported = Factory.Error(
        3,
        "[Service] cannot be applied to a class nested inside a generic type",
        "[Service] cannot be applied to '{0}' because it is nested inside a generic type; a generic class is only supported when all of its type parameters are its own",
        "Open generic support only covers classes whose generic context is entirely their own type parameter list. A class nested inside a generic type carries its containing type's parameters and cannot be registered.");

    public static readonly DiagnosticDescriptor DuplicateRegistration = Factory.Warning(
        4,
        "Duplicate service registration",
        "The service type '{0}' is registered more than once for implementation '{1}'{2}",
        "The same (service type, implementation type, key) combination is registered by more than one [Service] attribute across the compilation.",
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor KeyedTryAddEnumerableNotSupported = Factory.Error(
        5,
        "'Key' cannot be combined with RegistrationMode.TryAddEnumerable",
        "'Key' cannot be combined with RegistrationMode.TryAddEnumerable on '{0}' because Microsoft.Extensions.DependencyInjection has no corresponding keyed API",
        "There is no TryAddEnumerable equivalent for keyed services in Microsoft.Extensions.DependencyInjection.");

    public static readonly DiagnosticDescriptor SelfTryAddEnumerableNotSupported = Factory.Error(
        6,
        "RegistrationMode.TryAddEnumerable cannot register a type as itself",
        "'{0}' cannot be registered via RegistrationMode.TryAddEnumerable as its own service type because Microsoft.Extensions.DependencyInjection cannot distinguish duplicate entries; implement an interface or specify a different service type via 'As'",
        "TryAddEnumerable(ServiceDescriptor.Singleton<T, T>()) throws ArgumentException at runtime because Microsoft.Extensions.DependencyInjection cannot tell distinct registrations of the same implementation type apart when the service type and implementation type are identical.");

    public static readonly DiagnosticDescriptor InaccessibleType = Factory.Error(
        7,
        "[Service] type must be accessible to generated code",
        "'{0}' cannot be registered because it is not accessible from the generated registration code; make the type (and its containing types) at least 'internal' and not file-local",
        "The generated registration extension method lives in a separate file, in the Microsoft.Extensions.DependencyInjection namespace, in the same assembly; it can only reference types -- including a 'typeof(...)' Key value and any generic type arguments -- that are (along with their containing types) at least internal and not file-local.");

    public static readonly DiagnosticDescriptor UndefinedEnumValue = Factory.Error(
        8,
        "Undefined enum value on [Service]",
        "The value '{0}' is not a defined '{1}' value",
        "The Lifetime and Mode arguments of [Service] must be one of the values defined by ServiceLifetime/RegistrationMode; an out-of-range value (e.g. from an explicit numeric cast) is silently mishandled otherwise.");

    public static readonly DiagnosticDescriptor OpenGenericServiceTypeNotExactMatch = Factory.Error(
        9,
        "Open generic service type must use the class's own type parameters",
        "'{0}' cannot be registered as '{1}' because an open generic class can only be registered as itself or as an implemented interface or base class whose type arguments are exactly the class's own type parameters in declaration order",
        "Microsoft.Extensions.DependencyInjection resolves an open generic registration by substituting the requested service type's arguments positionally into the implementation type. Any other shape (a closed or non-generic service type, reordered, partially used, or nested type arguments) either cannot be constructed or produces a type that does not implement the requested service, so it is rejected at compile time.");

    public static readonly DiagnosticDescriptor OpenGenericInstanceNotShared = Factory.Warning(
        10,
        "Open generic registrations do not share an instance across service types",
        "'{0}' is registered as {1} service types as an open generic; each closed service type will resolve to a separate instance because open generic registrations cannot use forwarding factories",
        "For a non-generic class, a Singleton/Scoped registration against multiple service types shares one instance via forwarding factories. Open generic registrations cannot use factories, so every service type gets an independent registration and instances are not shared. Suppress this warning if separate instances are intended.");

    public static readonly DiagnosticDescriptor FactoryMethodNotFound = Factory.Error(
        11,
        "'Factory' method not found",
        "No ordinary method named '{0}' is declared on '{1}'",
        "The 'Factory' argument of [Service] must name an ordinary (non-property, non-operator) method declared directly on the decorated class. This also covers an empty-string 'Factory' value, which never matches any method.");

    public static readonly DiagnosticDescriptor FactoryMethodInvalid = Factory.Error(
        12,
        "'Factory' method has an unusable signature",
        "One or more methods named '{0}' are declared on '{1}', but none has a usable signature: a factory method must be static, non-generic, have no parameters or a single 'System.IServiceProvider' parameter, and return exactly '{1}'",
        "A method named by 'Factory' was found, but no overload of it is static, non-generic, parameterless-or-single-IServiceProvider-parameter, and returns exactly the decorated class.");

    public static readonly DiagnosticDescriptor FactoryOnOpenGenericNotSupported = Factory.Error(
        13,
        "'Factory' cannot be used on an open generic class",
        "'Factory' cannot be used on open generic class '{0}' because Microsoft.Extensions.DependencyInjection has no factory-based registration for open generics",
        "Microsoft.Extensions.DependencyInjection's open generic registration overloads (Type-based, not <TService, TImplementation>) have no factory-delegate counterpart, so an open generic class cannot combine [Service] with a 'Factory'.");

    public static readonly DiagnosticDescriptor FactoryMethodInaccessible = Factory.Error(
        14,
        "'Factory' method is not accessible to generated code",
        "The factory method '{0}.{1}' is not accessible from the generated registration code; it must be at least 'internal'",
        "The chosen factory method is invoked from the generated registration extension method, which lives in a separate file in the same assembly; a private or protected factory method cannot be called from there.");

    public static readonly DiagnosticDescriptor ConflictingImplementations = Factory.Warning(
        15,
        "Multiple implementations registered for the same service type",
        "The service type '{0}'{1} is registered with {2} different implementation types ({3}); a single-instance resolution returns whichever of them is registered last, and the generator emits registrations ordered by implementation type name rather than by source order",
        "Microsoft.Extensions.DependencyInjection resolves a single service instance from the last matching registration, and this generator emits registrations sorted by implementation type name, so which implementation wins is decided by type naming rather than by the order the [Service] attributes appear in source -- renaming a class can silently change which one is resolved. If several implementations are meant to be injected together as IEnumerable<T>, register every one of them with RegistrationMode.TryAddEnumerable (a group consisting only of TryAddEnumerable registrations is never reported); otherwise disambiguate them with distinct 'Key' values, or suppress this warning if one deliberately overrides the other.",
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor ServiceFactoryTargetNotInterface = Factory.Error(
        16,
        "[ServiceFactory] can only be applied to an interface",
        "[ServiceFactory] cannot be applied to '{0}' because it is {1}; only an interface can declare a service factory",
        "The generator implements a [ServiceFactory] target by emitting a class that implements it, which is only possible for an interface. [AttributeUsage(AttributeTargets.Interface)] already makes any other target a CS0592 compiler error, so this rule only ever fires alongside that error, as a defence against the attribute's usage being widened without the generator following suit.");

    public static readonly DiagnosticDescriptor ServiceFactoryMemberShapeInvalid = Factory.Error(
        17,
        "[ServiceFactory] interface must declare exactly one method",
        "[ServiceFactory] interface '{0}' must declare exactly one member and that member must be an ordinary, non-static method, but {1}",
        "The generated implementation only knows how to implement a single enum-keyed lookup method. Any additional member -- another method, a property, an event, a field, or a nested type -- would be left unimplemented, so the whole interface is rejected rather than generating code that does not compile.");

    public static readonly DiagnosticDescriptor ServiceFactoryMethodSignatureInvalid = Factory.Error(
        18,
        "[ServiceFactory] method must take a single enum parameter and return a service type",
        "The method '{0}.{1}' cannot be used as a service factory because {2}; it must be non-generic, take exactly one by-value parameter of an enum type, and return a non-void service type",
        "The generated implementation forwards its single parameter verbatim as the service key to GetRequiredKeyedService<TReturn>, which requires exactly one by-value enum parameter and a non-void, non-by-ref return type. A generic method has no single closed form to implement against.");

    public static readonly DiagnosticDescriptor ServiceFactoryGenericNotSupported = Factory.Error(
        19,
        "[ServiceFactory] cannot be applied to a generic interface or one nested inside a generic type",
        "[ServiceFactory] cannot be applied to '{0}' because it is {1}; a service factory interface must be non-generic and not nested inside a generic type",
        "The generated implementation is a non-generic class registered as a singleton against one closed service type. An open generic factory interface -- or a non-generic one that inherits type parameters from a containing generic type -- has no single closed form to register, so it is rejected at compile time.");

    public static readonly DiagnosticDescriptor ServiceFactoryInaccessibleType = Factory.Error(
        20,
        "[ServiceFactory] type must be accessible to generated code",
        "'{0}' cannot be used by the service factory generated for '{1}' because it is not accessible from the generated code; make the type (and its containing types) at least 'internal' and not file-local",
        "The generated factory implementation lives in a separate file, in the SsalKit.DependencyInjection.Generated namespace, in the same assembly; it can only name the factory interface, its method's enum parameter type, and its return type when each of those (along with their containing types) is at least internal and not file-local.");

    public static readonly DiagnosticDescriptor ContractNotInterface = Factory.Error(
        21,
        "[RegisterImplementationsOf] contract must be an interface",
        "'{0}' cannot be used as a [RegisterImplementationsOf] contract because it is {1}; only an interface can be scanned for implementations",
        "A convention scan registers each matched class against the contract it implements, which is only a meaningful relationship for an interface. A base class, a struct, an enum, or a delegate type has no set of 'implementations' the generator can discover, so the declaration is rejected rather than silently matching nothing.");

    public static readonly DiagnosticDescriptor ContractMatchedNothing = Factory.Warning(
        22,
        "[RegisterImplementationsOf] contract matched no class in this assembly",
        "No class in this assembly is registered for the [RegisterImplementationsOf] contract '{0}'; the convention scan only sees classes declared in the compilation it is applied to, never ones in referenced assemblies",
        "A contract that matches nothing registers nothing, and would otherwise fail completely silently -- the usual causes being a misspelled or wrong-namespace interface, an expectation that classes in a referenced assembly would be discovered (they are not: declare the attribute in that assembly instead), or every candidate class having been skipped because it is abstract, static, inaccessible from the generated code, nested inside a generic type, or already decorated with [Service].",
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor DuplicateContract = Factory.Error(
        23,
        "Duplicate [RegisterImplementationsOf] contract",
        "The contract '{0}' is already declared by another [assembly: RegisterImplementationsOf]; only the first declaration is used",
        "Two declarations of the same contract have no combined meaning: with differing lifetimes or modes there is no rule for which should win, and with identical ones the second is pure duplication (silently discarded at runtime by TryAddEnumerable, but a doubled registration under any other mode). Keep one declaration, and use [Service] on the individual classes that need to deviate from it.");

    public static readonly DiagnosticDescriptor UndefinedContractEnumValue = Factory.Error(
        24,
        "Undefined enum value on [RegisterImplementationsOf]",
        "The value '{0}' is not a defined '{1}' value",
        "The Lifetime and Mode arguments of [RegisterImplementationsOf] must be one of the values defined by ServiceLifetime/RegistrationMode; an out-of-range value (e.g. from an explicit numeric cast) is silently mishandled otherwise.");

    public static readonly DiagnosticDescriptor ContractInaccessibleType = Factory.Error(
        25,
        "[RegisterImplementationsOf] contract must be accessible to generated code",
        "'{0}' cannot be used as a [RegisterImplementationsOf] contract because it is not accessible from the generated registration code; make the type (and its containing types) at least 'internal' and not file-local",
        "The generated registration extension method lives in a separate file, in the Microsoft.Extensions.DependencyInjection namespace, in the same assembly; every service type it names -- including a convention scan's contract -- must be (along with its containing types and generic type arguments) at least internal and not file-local.");

    public static readonly DiagnosticDescriptor ConflictingContractRegistrations = Factory.Warning(
        26,
        "Overlapping [RegisterImplementationsOf] contracts register the same implementation differently",
        "'{1}' is registered as '{0}' by {2} overlapping [RegisterImplementationsOf] contracts that do not agree on lifetime and mode; both registrations are emitted",
        "Two contracts can overlap -- typically an unbound generic one (typeof(IHandler<>)) alongside a closed instantiation of it (typeof(IHandler<int>)) -- and match the same class under the same service type. Identical registrations produced this way are collapsed into one and reported nothing; registrations that disagree on lifetime or mode cannot be collapsed, so both are emitted and which one wins is decided by Microsoft.Extensions.DependencyInjection's own rules rather than by anything in the declarations.",
        WellKnownDiagnosticTags.CompilationEnd);
}
