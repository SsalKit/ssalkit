namespace SsalKit.Guard;

/// <summary>
/// Registers an exception type you do not own — a BCL or third-party exception — with a mapping
/// container, so it participates in the generated lookup even though it cannot carry
/// <see cref="ErrorCodeAttribute{TCode}"/> itself.
/// </summary>
/// <typeparam name="TCode">
/// The enum type of the container this registration belongs to. It must match the container's own
/// <see cref="ErrorCodesAttribute{TCode}"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// Applied to the mapping container rather than to the exception, and repeatable: in practice a
/// real boundary's mapping table is largely made of exceptions from other people's libraries —
/// cache timeouts, cluster failures, token validation — and this is where those get their codes.
/// </para>
/// <para>
/// Externally registered types take part in the lookup only. No factory or throw helper is
/// generated for them, because this library cannot vouch for the constructor contract of a type it
/// does not own.
/// </para>
/// <para>
/// <see cref="ExceptionType"/> must derive from <see cref="Exception"/>, and registering the same
/// exception type twice in one container — whether twice here, or here and again through its own
/// <see cref="ErrorCodeAttribute{TCode}"/> — is a compile-time error rather than a silent
/// precedence rule.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ErrorCodes&lt;GameStatusCode&gt;]
/// [ExternalErrorCode&lt;GameStatusCode&gt;(typeof(RedisTimeoutException), GameStatusCode.ServerBusy)]
/// [ExternalErrorCode&lt;GameStatusCode&gt;(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
/// public static partial class GameErrors;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ExternalErrorCodeAttribute<TCode> : Attribute
    where TCode : struct, Enum
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalErrorCodeAttribute{TCode}"/> class.
    /// </summary>
    /// <param name="exceptionType">
    /// The exception type being registered. It must derive from <see cref="Exception"/>.
    /// </param>
    /// <param name="code">The error code that exception type maps to.</param>
    public ExternalErrorCodeAttribute(Type exceptionType, TCode code)
    {
        ExceptionType = exceptionType;
        Code = code;
    }

    /// <summary>
    /// Gets the exception type being registered.
    /// </summary>
    public Type ExceptionType { get; }

    /// <summary>
    /// Gets the error code the registered exception type maps to.
    /// </summary>
    public TCode Code { get; }
}
