namespace SsalKit.Guard;

/// <summary>
/// Marks a <see langword="static"/> <see langword="partial"/> class as the mapping container for a
/// code enum, letting the SsalKit.Guard source generator fill in the exception → code lookup and
/// the per-code helpers.
/// </summary>
/// <typeparam name="TCode">
/// The enum type this container maps to. Only exceptions carrying
/// <see cref="ErrorCodeAttribute{TCode}"/> with the same <typeparamref name="TCode"/>, plus this
/// container's own <see cref="ExternalErrorCodeAttribute{TCode}"/> registrations, take part —
/// so several containers with different code enums can coexist in one assembly. One class maps one
/// enum: a second application with a different <typeparamref name="TCode"/> is a duplicate-attribute
/// error (CS0579), since <c>AllowMultiple</c> is enforced against this attribute's generic
/// definition rather than against each constructed form of it.
/// </typeparam>
/// <remarks>
/// <para>
/// The generator emits into the container a <c>TryMap</c> lookup, a <c>MapOrDefault</c>
/// convenience overload, and — for every decorated exception in the compilation that exposes a
/// recognised public constructor — a factory and a <c>[DoesNotReturn]</c> throw helper named after
/// the code.
/// </para>
/// <para>
/// The lookup is ordered by inheritance depth, deepest first, so a derived exception is always
/// matched before its base. That is the whole point of generating it: a hand-written
/// <c>switch</c> has to place derived types first and stays correct only as long as everyone
/// remembers to, and forgetting to register a newly added exception at all is something the
/// compiler cannot notice.
/// </para>
/// <para>
/// The generated part is a second file, so the container must be a <see langword="static"/>
/// <see langword="partial"/> class that such a file can attach to and name: not
/// <c>file</c>-local, nested only inside <see langword="partial"/> types, and neither generic nor
/// nested inside a generic type (nor mapping a <typeparamref name="TCode"/> that is). Violations
/// are reported as compile-time errors.
/// </para>
/// <para>
/// Only this compilation is visible to the generator: an exception carrying
/// <see cref="ErrorCodeAttribute{TCode}"/> in a referenced assembly is not collected, so a
/// container belongs in the same project as its exceptions. Types from elsewhere are registered
/// explicitly with <see cref="ExternalErrorCodeAttribute{TCode}"/>, which does cross assembly
/// boundaries.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ErrorCodes&lt;GameStatusCode&gt;]
/// [ExternalErrorCode&lt;GameStatusCode&gt;(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
/// public static partial class GameErrors;
///
/// // Generated:
/// //   GameErrors.TryMap(exception, out GameStatusCode code)
/// //   GameErrors.MapOrDefault(exception, GameStatusCode.Unknown)
/// //   GameErrors.UserNotFound("no such user")     // factory
/// //   GameErrors.ThrowUserNotFound("no such user") // [DoesNotReturn]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ErrorCodesAttribute<TCode> : Attribute
    where TCode : struct, Enum;
