namespace SsalKit.Guard;

/// <summary>
/// Declares the error code that an exception type maps to.
/// </summary>
/// <typeparam name="TCode">
/// The enum type the code belongs to. A codebase can use several unrelated code enums side by
/// side; each one gets its own mapping container (see <see cref="ErrorCodesAttribute{TCode}"/>),
/// and only exceptions declaring the matching <typeparamref name="TCode"/> take part in it. One
/// exception declares one code: a second application with a different <typeparamref name="TCode"/>
/// is a duplicate-attribute error (CS0579), since <c>AllowMultiple</c> is enforced against this
/// attribute's generic definition rather than against each constructed form of it.
/// </typeparam>
/// <remarks>
/// <para>
/// The code lives on the <i>type</i>, not on the instance: an exception carries no code field, and
/// the generated mapping container answers "which code is this exception?" at the boundary. That
/// keeps one code paired with one declared exception type, so the type is the thing you
/// <c>catch</c>, the thing you document, and the thing the compiler can check.
/// </para>
/// <para>
/// It may only be applied to a non-abstract, non-generic class deriving from
/// <see cref="ErrorCodedException"/>; anything else is a compile-time error. Decorated exceptions
/// also get strongly-typed factory and throw helpers on the mapping container, provided the
/// exception exposes one of the recognised public constructor shapes.
/// </para>
/// <para>
/// An enum is required rather than loose <see cref="int"/> constants so that a code is
/// self-describing and checkable at compile time. Converting the enum to whatever the transport
/// speaks — an HTTP status, a gRPC code, a wire integer — is the consuming side's job, which is
/// exactly why the mapping returns <typeparamref name="TCode"/> and not a transport type.
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
/// <param name="code">The error code the decorated exception type maps to.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ErrorCodeAttribute<TCode>(TCode code) : Attribute
    where TCode : struct, Enum
{
    /// <summary>
    /// Gets the error code the decorated exception type maps to.
    /// </summary>
    public TCode Code { get; } = code;
}
