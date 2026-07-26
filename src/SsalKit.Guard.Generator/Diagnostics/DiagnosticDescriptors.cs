using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Diagnostics;

/// <summary>
/// The <c>SSALG</c> diagnostic table reported by <see cref="ErrorCodesGenerator"/>.
/// </summary>
/// <remarks>
/// <para>
/// The rules split into three groups by what the generator does after reporting them. A rule about
/// a single <i>registration</i> (SSALG001, SSALG004, SSALG005, SSALG009, SSALG010) drops that
/// registration and leaves the rest of the container intact, because one mis-declared exception
/// should not take the whole mapping table down with it. A rule about the <i>container</i>
/// (SSALG002, SSALG007) or about an ambiguity the generator refuses to resolve on the user's behalf
/// (SSALG003) suppresses that container's generated file entirely.
/// </para>
/// <para>
/// The warnings describe something that still compiles but is very likely not what was meant: a
/// decorated exception with no usable constructor (SSALG006) still takes part in the mapping and
/// only loses its helpers; an exception whose code enum has no container at all (SSALG008) is the
/// silent-no-op case -- everything looks declared, yet nothing is generated anywhere; an
/// <c>[ExternalErrorCode]</c> written for the wrong code enum (SSALG010) would otherwise be dropped
/// without a word; and a container whose code enum lives in another assembly with nothing to fill
/// it (SSALG011) is the cross-assembly shape of the same silence.
/// </para>
/// </remarks>
internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSALG", "SsalKit.Guard");

    /// <summary>
    /// SSALG001: <c>[ErrorCode]</c> on a type that does not derive from <c>ErrorCodedException</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor ExceptionMustDeriveFromErrorCodedException = Factory.Error(
        1,
        "[ErrorCode] requires an ErrorCodedException",
        "[ErrorCode] cannot be applied to '{0}' because it does not derive from 'SsalKit.Guard.ErrorCodedException'; the registration is ignored",
        "'ErrorCodedException' is the compile-time anchor of the pattern: it is what lets a consumer separate domain failures from everything else with a single 'catch (ErrorCodedException)', and what guarantees every code-carrying type shares the same side-effect-free constructor contract. An exception type you do not own cannot derive from it -- register that one on the container with [ExternalErrorCode] instead.");

    /// <summary>
    /// SSALG002: the <c>[ErrorCodes]</c> container is not a <c>static partial class</c> that a
    /// generated file can attach a second part to. Message argument 1 names every specific reason.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainerMustBeStaticPartialClass = Factory.Error(
        2,
        "[ErrorCodes] container must be a static partial class",
        "'{0}' cannot be an error-code mapping container because it is {1}; declare it as a 'static partial class'",
        "The generator emits the mapping table and the code helpers as a second part of the container, so the container has to be 'partial' for that part to attach to it, and 'static' because every generated member is static and the type holds no state of its own. The generated part is a separate file, so a 'file'-local container is rejected too -- a second part written in another file would declare a different type, silently leaving the hand-written one empty -- and every type the container is nested in has to be 'partial' as well, since the generated file has to re-declare the whole nesting chain.");

    /// <summary>
    /// SSALG003: the same exception type is registered twice in one container -- twice through
    /// <c>[ExternalErrorCode]</c>, or once there and once through its own <c>[ErrorCode]</c>.
    /// Reported on every registration involved, and the container is not generated.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateRegistration = Factory.Error(
        3,
        "Duplicate error-code registration",
        "'{0}' is registered more than once in the mapping container '{1}'; remove all but one registration",
        "Two registrations for one exception type mean two candidate codes, and picking a winner by declaration order or by attribute kind would be a silent precedence rule that nobody reading the code could see. Note that a type carrying [ErrorCode] is already registered in every container for its code enum, so re-registering it with [ExternalErrorCode] is always a duplicate. No mapping is generated for a container with an ambiguous registration.");

    /// <summary>
    /// SSALG004: an <c>[ExternalErrorCode]</c> registration names a type the generated lookup cannot
    /// test for. Message argument 2 carries the reason.
    /// </summary>
    public static readonly DiagnosticDescriptor ExternalTypeMustBeAnException = Factory.Error(
        4,
        "[ExternalErrorCode] requires an exception type",
        "[ExternalErrorCode] cannot register '{0}' in '{1}' because {2}; the registration is ignored",
        "Every registration becomes a type test against the exception passed to the generated 'TryMap'. A type that cannot appear as the runtime type of a thrown exception -- anything not deriving from 'System.Exception' -- would produce a test that can never succeed, and an unbound generic type ('typeof(Foo<>)') cannot be written as a type test at all.");

    /// <summary>
    /// SSALG005: the decorated exception cannot be named or instantiated by the generated code.
    /// Message argument 1 names the reason.
    /// </summary>
    public static readonly DiagnosticDescriptor ExceptionMustBeConcreteAndNonGeneric = Factory.Error(
        5,
        "[ErrorCode] exception must be concrete and non-generic",
        "[ErrorCode] cannot be applied to '{0}' because it is {1}; the registration is ignored",
        "The generated container names the exception type in a type test and, for the helpers, in a 'new' expression. An abstract type cannot be constructed, and an open generic type (or one nested inside a generic type) has no single closed form to write there -- while a closed generic exception can still be registered on the container with [ExternalErrorCode].");

    /// <summary>
    /// SSALG006: the decorated exception exposes none of the recognised public constructor shapes,
    /// so it gets no factory and no throw helper. It still takes part in the mapping table.
    /// </summary>
    public static readonly DiagnosticDescriptor NoRecognisedConstructor = Factory.Warning(
        6,
        "No factory or throw helper is generated for the exception",
        "'{0}' takes part in the mapping table of '{1}', but no factory or throw helper is generated for it because it declares none of the recognised public constructors '()', '(string?)' and '(string?, System.Exception?)'",
        "The generated helpers mirror one of the exception's own public constructors, so that 'throw Errors.Something(message)' constructs exactly what 'new SomethingException(message)' would. A type whose constructors all take other parameters has no shape to mirror; add one of the recognised constructors, or construct the exception directly.");

    /// <summary>
    /// SSALG007: the container is generic or nested inside a generic type, or its code enum is
    /// nested inside a generic type. Message argument 1 carries the reason.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainerCannotBeGeneric = Factory.Error(
        7,
        "[ErrorCodes] container and code enum cannot be generic",
        "'{0}' cannot be an error-code mapping container because {1}",
        "The generated part has to re-declare the container and every type containing it, and a generic container would have to repeat its type parameters and constraints there -- while adding nothing: the mapping table is the same for every instantiation, and 'Errors<T>.TryMap' would force every call site to state a T that does not matter. A code enum nested inside a generic type is rejected for a related reason: its name is written into the generated documentation and into a 'cref', where the type arguments would have to be escaped as XML rather than as C#. Use a non-generic container and a code enum that is not nested inside a generic type.");

    /// <summary>
    /// SSALG008: a decorated exception's code enum has no <c>[ErrorCodes]</c> container anywhere in
    /// the compilation, so nothing at all is generated for it.
    /// </summary>
    public static readonly DiagnosticDescriptor NoContainerForCodeEnum = Factory.Warning(
        8,
        "No mapping container for the declared code enum",
        "'{0}' declares an error code of type '{1}', but this compilation has no [ErrorCodes<{1}>] container for that enum, so no mapping, factory or throw helper is generated for it",
        "A code declared with [ErrorCode] only becomes something you can call or map through a container. Without one, every declaration still compiles and nothing is generated anywhere, which is indistinguishable from the generator not running at all. Add a 'static partial class' marked [ErrorCodes<TCode>] for the enum, or remove the attribute.");

    /// <summary>
    /// SSALG009: the decorated exception is not visible from the file the container is generated
    /// into, so naming it there would not compile. Message argument 1 carries the reason.
    /// </summary>
    public static readonly DiagnosticDescriptor ExceptionMustBeAccessible = Factory.Error(
        9,
        "[ErrorCode] exception type is not accessible to the generated code",
        "[ErrorCode] cannot be applied to '{0}' because {1}, so the generated mapping container cannot name it; the registration is ignored",
        "The container's generated part is a separate file, and every registration becomes a type test -- and, for a decorated exception, a 'new' expression -- written out in it. A type that is private, protected, private protected, or 'file'-local is not nameable from there, so including it would produce a generated file that does not compile: an error pointing at code the user never wrote. Declare the exception 'internal' or 'public', or drop the attribute.");

    /// <summary>
    /// SSALG010: an <c>[ExternalErrorCode&lt;TCode&gt;]</c> registration names a different code enum
    /// from the container's own <c>[ErrorCodes&lt;TCode&gt;]</c>, so it belongs to no container at
    /// all.
    /// </summary>
    public static readonly DiagnosticDescriptor ExternalRegistrationForAnotherCodeEnum = Factory.Warning(
        10,
        "[ExternalErrorCode] does not match the container's code enum",
        "[ExternalErrorCode<{0}>] on '{1}' is ignored because the container maps codes of type '{2}'; change the registration's code enum to '{2}'",
        "A class can carry only one [ErrorCodes<TCode>] -- [AttributeUsage(AllowMultiple = false)] is enforced against the attribute's generic definition, so a second application with a different TCode is a duplicate-attribute error at the declaration site. An [ExternalErrorCode<TCode>] naming any other enum therefore has no container to join, and is always a typo rather than a registration for some other container.");

    /// <summary>
    /// SSALG011: the container's code enum comes from another assembly and nothing in this
    /// compilation registers anything in it, which is what a cross-assembly arrangement looks like
    /// when the <c>[ErrorCode]</c> exceptions the user expected to be collected are in that other
    /// assembly.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainerForAnotherAssemblysCodeEnumIsEmpty = Factory.Warning(
        11,
        "Mapping container for another assembly's code enum has no registrations",
        "'{0}' maps '{1}', which is declared in '{2}', but this compilation registers nothing in it, so the generated mapping is empty",
        "The generator only sees the compilation it runs in: an exception carrying [ErrorCode] in another assembly is invisible here, and a container placed alongside the code enum's consumers rather than alongside its exceptions collects nothing. Move the container into the assembly that declares the [ErrorCode] exceptions, or register the types you want mapped explicitly with [ExternalErrorCode], which works across assembly boundaries.");
}
