namespace SsalKit.Guard;

/// <summary>
/// Base class for domain exceptions that can be mapped to an error code at the application
/// boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>The code is not carried on the instance.</b> An error code is declared <i>on the exception
/// type</i> with <c>[ErrorCode&lt;TCode&gt;]</c>, and a generated mapping container resolves
/// type → code at the boundary. Throwing an arbitrary code with an otherwise anonymous exception
/// (<c>throw new SomeException(4001, "...")</c>) is deliberately not supported: in this library a
/// code always corresponds to a declared exception type, so the type itself is both the
/// documentation and the <c>catch</c> target.
/// </para>
/// <para>
/// <b>An exception is pure data.</b> No constructor here has a side effect — nothing is tagged on
/// <see cref="System.Diagnostics.Activity"/>, nothing is logged, no metric is emitted. Creating an
/// exception must stay free of observability coupling: the moment an exception is constructed is
/// not the moment it is handled, a caught exception may be rethrown or wrapped later, and tests
/// that merely construct one must not perturb ambient telemetry. Tagging, logging, and transport
/// mapping belong to the consuming side (an exception filter, middleware, or a
/// <c>catch</c> block), which is also the only place that knows the surrounding request context.
/// </para>
/// <para>
/// This type exists for two reasons: it is the compile-time anchor that
/// <c>[ErrorCode&lt;TCode&gt;]</c> is validated against (the attribute may only be applied to types
/// deriving from it), and it lets a consumer separate domain failures from everything else with a
/// single <c>catch (ErrorCodedException)</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public enum GameStatusCode { UserNotFound = 1001 }
///
/// [ErrorCode&lt;GameStatusCode&gt;(GameStatusCode.UserNotFound)]
/// public sealed class UserNotFoundException : ErrorCodedException
/// {
///     public UserNotFoundException(string? message = null) : base(message) { }
/// }
/// </code>
/// </example>
public abstract class ErrorCodedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCodedException"/> class with a
    /// runtime-supplied default message.
    /// </summary>
    protected ErrorCodedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCodedException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">
    /// The message that describes the error, or <see langword="null"/> to use a runtime-supplied
    /// default message.
    /// </param>
    protected ErrorCodedException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCodedException"/> class with a specified
    /// error message and the exception that caused it.
    /// </summary>
    /// <param name="message">
    /// The message that describes the error, or <see langword="null"/> to use a runtime-supplied
    /// default message.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or <see langword="null"/> if none
    /// is specified.
    /// </param>
    protected ErrorCodedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
