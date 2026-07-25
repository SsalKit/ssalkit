namespace SsalKit.Guard;

/// <summary>
/// The exception thrown when a <see cref="Guard"/> clause fails.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="Guard"/> overload that does not take an explicit exception factory throws this
/// type, with a message that embeds the caller's own expression text captured by
/// <see cref="System.Runtime.CompilerServices.CallerArgumentExpressionAttribute"/>.
/// </para>
/// <para>
/// It derives from <see cref="ErrorCodedException"/> so a guard failure is a domain failure like
/// any other: give it a code in your own mapping container with
/// <c>[ExternalErrorCode&lt;TCode&gt;(typeof(GuardViolationException), ...)]</c> and it maps to
/// whatever "internal invariant violated" code your transport uses.
/// </para>
/// <para>
/// The type carries no state beyond <see cref="Exception"/>'s own and, like its base, its
/// constructors have no side effects.
/// </para>
/// </remarks>
public sealed class GuardViolationException : ErrorCodedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GuardViolationException"/> class with a
    /// runtime-supplied default message.
    /// </summary>
    public GuardViolationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardViolationException"/> class with a
    /// specified error message.
    /// </summary>
    /// <param name="message">
    /// The message that describes the guard violation, or <see langword="null"/> to use a
    /// runtime-supplied default message.
    /// </param>
    public GuardViolationException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardViolationException"/> class with a
    /// specified error message and the exception that caused it.
    /// </summary>
    /// <param name="message">
    /// The message that describes the guard violation, or <see langword="null"/> to use a
    /// runtime-supplied default message.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or <see langword="null"/> if none
    /// is specified.
    /// </param>
    public GuardViolationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
