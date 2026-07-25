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
/// so several containers with different code enums can coexist in one assembly.
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
/// The container must be a <see langword="static"/> <see langword="partial"/> class that is
/// neither generic nor nested inside a generic type; violations are reported as compile-time
/// errors.
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
