using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SsalKit.Guard;

/// <summary>
/// Static guard clauses for domain invariants and state preconditions, throwing
/// <see cref="GuardViolationException"/> with the caller's own expression text embedded in the
/// message.
/// </summary>
/// <remarks>
/// <para>
/// <b>Static entry point, not extension methods.</b> Guards are exposed as <c>Guard.Xxx(...)</c>
/// rather than <c>value.ThrowIfXxx(...)</c> on purpose: a <c>this T</c> extension would surface on
/// the IntelliSense of every single type in a consuming codebase. Reading <c>Guard.</c> at the call
/// site also states plainly that a check is happening.
/// </para>
/// <para>
/// <b>Failure context is captured, not typed out.</b> Every clause takes a trailing
/// <see cref="CallerArgumentExpressionAttribute"/> parameter, so the source text of the checked
/// expression lands in the message for free — no hand-maintained list of "here are the values that
/// mattered" that drifts from the condition it describes.
/// </para>
/// <para>
/// <b>This is not argument validation.</b> For public-API parameter checks the BCL already has
/// <see cref="ArgumentNullException.ThrowIfNull(object?, string?)"/>,
/// <see cref="ArgumentException.ThrowIfNullOrWhiteSpace(string?, string?)"/>, and the
/// <c>ArgumentOutOfRangeException.ThrowIf*</c> family, which throw the exception types callers and
/// analyzers expect from an argument contract. <see cref="Guard"/> deliberately does not duplicate
/// them. It is for <i>domain</i> invariants — "this aggregate must still be in a state that allows
/// this operation" — whose failure is a domain error that maps to an error code, not an
/// <see cref="ArgumentException"/>.
/// </para>
/// <para>
/// <b>Message contract.</b> A failing clause produces
/// <c>Guard.{Clause} ({expression}) failed.</c> for <see cref="That(bool, string?)"/> and
/// <c>Guard.{Clause} ({expression}) failed: {detail}</c> for the rest. When the expression text is
/// unavailable — the only way that happens is a caller explicitly passing <see langword="null"/>
/// or the empty string, or an invocation from a language that does not honour
/// <see cref="CallerArgumentExpressionAttribute"/> — the placeholder
/// <c>&lt;expression unavailable&gt;</c> is used instead.
/// </para>
/// <para>
/// <b>The success path allocates nothing.</b> Messages are composed only after a check has already
/// failed, and the <see cref="Func{TResult}"/> overloads invoke the factory only on failure.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Guard.That(order.Status == OrderStatus.Open);
/// // throws: Guard.That (order.Status == OrderStatus.Open) failed.
///
/// var owner = Guard.NotNull(world.FindPlayer(id));
/// // throws: Guard.NotNull (world.FindPlayer(id)) failed: value was null.
///
/// Guard.That(balance >= amount, () =&gt; new InsufficientFundsException(balance, amount));
/// </code>
/// </example>
public static class Guard
{
    /// <summary>
    /// The placeholder substituted for the expression text when the caller did not supply one.
    /// </summary>
    internal const string UnknownExpression = "<expression unavailable>";

    /// <summary>
    /// Throws a <see cref="GuardViolationException"/> when <paramref name="condition"/> is
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The invariant that must hold.</param>
    /// <param name="expression">
    /// Automatically populated by the compiler with the source text of
    /// <paramref name="condition"/>. Do not pass this explicitly.
    /// </param>
    /// <exception cref="GuardViolationException">
    /// <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    public static void That(
        bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            throw new GuardViolationException(BuildMessage(nameof(That), expression));
        }
    }

    /// <summary>
    /// Throws the exception produced by <paramref name="exceptionFactory"/> when
    /// <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The invariant that must hold.</param>
    /// <param name="exceptionFactory">
    /// Produces the exception to throw. It is invoked only when the check fails, so building a
    /// rich exception costs nothing on the success path.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="exceptionFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="GuardViolationException">
    /// The check failed and <paramref name="exceptionFactory"/> returned <see langword="null"/>.
    /// </exception>
    public static void That(bool condition, Func<Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);

        if (!condition)
        {
            throw Materialize(exceptionFactory, nameof(That));
        }
    }

    /// <summary>
    /// Throws a <see cref="GuardViolationException"/> when <paramref name="value"/> is
    /// <see langword="null"/>; otherwise returns it as a non-nullable reference.
    /// </summary>
    /// <typeparam name="T">The reference type being checked.</typeparam>
    /// <param name="value">The value that must not be <see langword="null"/>.</param>
    /// <param name="expression">
    /// Automatically populated by the compiler with the source text of <paramref name="value"/>.
    /// Do not pass this explicitly.
    /// </param>
    /// <returns><paramref name="value"/>, known to be non-<see langword="null"/>.</returns>
    /// <exception cref="GuardViolationException">
    /// <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static T NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : class
    {
        if (value is null)
        {
            throw new GuardViolationException(BuildMessage(nameof(NotNull), expression, "value was null."));
        }

        return value;
    }

    /// <summary>
    /// Throws the exception produced by <paramref name="exceptionFactory"/> when
    /// <paramref name="value"/> is <see langword="null"/>; otherwise returns it as a non-nullable
    /// reference.
    /// </summary>
    /// <typeparam name="T">The reference type being checked.</typeparam>
    /// <param name="value">The value that must not be <see langword="null"/>.</param>
    /// <param name="exceptionFactory">
    /// Produces the exception to throw. It is invoked only when the check fails.
    /// </param>
    /// <returns><paramref name="value"/>, known to be non-<see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="exceptionFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="GuardViolationException">
    /// The check failed and <paramref name="exceptionFactory"/> returned <see langword="null"/>.
    /// </exception>
    public static T NotNull<T>([NotNull] T? value, Func<Exception> exceptionFactory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);

        if (value is null)
        {
            throw Materialize(exceptionFactory, nameof(NotNull));
        }

        return value;
    }

    /// <summary>
    /// Throws a <see cref="GuardViolationException"/> when <paramref name="value"/> has no value;
    /// otherwise returns the underlying value.
    /// </summary>
    /// <typeparam name="T">The value type being checked.</typeparam>
    /// <param name="value">The nullable value that must have a value.</param>
    /// <param name="expression">
    /// Automatically populated by the compiler with the source text of <paramref name="value"/>.
    /// Do not pass this explicitly.
    /// </param>
    /// <returns>The underlying value of <paramref name="value"/>.</returns>
    /// <exception cref="GuardViolationException">
    /// <paramref name="value"/> has no value.
    /// </exception>
    public static T NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : struct
    {
        if (value is null)
        {
            throw new GuardViolationException(BuildMessage(nameof(NotNull), expression, "value was null."));
        }

        return value.Value;
    }

    /// <summary>
    /// Throws a <see cref="GuardViolationException"/> when <paramref name="value"/> is
    /// <see langword="null"/> or the empty string; otherwise returns it.
    /// </summary>
    /// <param name="value">The string that must be non-empty.</param>
    /// <param name="expression">
    /// Automatically populated by the compiler with the source text of <paramref name="value"/>.
    /// Do not pass this explicitly.
    /// </param>
    /// <returns><paramref name="value"/>, known to be non-<see langword="null"/> and non-empty.</returns>
    /// <exception cref="GuardViolationException">
    /// <paramref name="value"/> is <see langword="null"/> or the empty string.
    /// </exception>
    public static string NotNullOrEmpty(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new GuardViolationException(
                BuildMessage(nameof(NotNullOrEmpty), expression, "value was null or empty."));
        }

        return value;
    }

    /// <summary>
    /// Throws a <see cref="GuardViolationException"/> when <paramref name="value"/> is
    /// <see langword="null"/>, empty, or consists only of white-space characters; otherwise returns
    /// it.
    /// </summary>
    /// <param name="value">The string that must contain at least one non-white-space character.</param>
    /// <param name="expression">
    /// Automatically populated by the compiler with the source text of <paramref name="value"/>.
    /// Do not pass this explicitly.
    /// </param>
    /// <returns><paramref name="value"/>, known to contain a non-white-space character.</returns>
    /// <exception cref="GuardViolationException">
    /// <paramref name="value"/> is <see langword="null"/>, empty, or all white-space.
    /// </exception>
    public static string NotNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new GuardViolationException(
                BuildMessage(nameof(NotNullOrWhiteSpace), expression, "value was null, empty, or white-space."));
        }

        return value;
    }

    /// <summary>
    /// Throws a <see cref="GuardViolationException"/> when <paramref name="value"/> falls outside
    /// the inclusive range <c>[<paramref name="min"/>, <paramref name="max"/>]</c>; otherwise
    /// returns it.
    /// </summary>
    /// <typeparam name="T">The comparable type being checked.</typeparam>
    /// <param name="value">The value that must lie within the range.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="expression">
    /// Automatically populated by the compiler with the source text of <paramref name="value"/>.
    /// Do not pass this explicitly.
    /// </param>
    /// <returns><paramref name="value"/>, known to be within the range.</returns>
    /// <remarks>
    /// Both bounds are inclusive, and the message renders <paramref name="value"/>,
    /// <paramref name="min"/>, and <paramref name="max"/> with the invariant culture so a failure
    /// reads the same everywhere.
    /// </remarks>
    /// <exception cref="GuardViolationException">
    /// <paramref name="value"/> compares less than <paramref name="min"/> or greater than
    /// <paramref name="max"/>.
    /// </exception>
    public static T InRange<T>(
        T value,
        T min,
        T max,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new GuardViolationException(BuildMessage(
                nameof(InRange),
                expression,
                FormattableString.Invariant(
                    $"value {value} was outside the inclusive range [{min}, {max}].")));
        }

        return value;
    }

    /// <summary>
    /// Invokes a caller-supplied exception factory, substituting a
    /// <see cref="GuardViolationException"/> when it hands back <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <c>throw null;</c> raises a bare <see cref="NullReferenceException"/> whose stack trace says
    /// nothing about the guard that actually failed, so a null factory result is turned into an
    /// exception that names the clause instead.
    /// </remarks>
    private static Exception Materialize(Func<Exception> exceptionFactory, string guardName)
    {
        Exception? exception = exceptionFactory();

        if (exception is null)
        {
            return new GuardViolationException(
                $"Guard.{guardName} failed, but the supplied exception factory returned null.");
        }

        return exception;
    }

    /// <summary>
    /// Composes the failure message for a clause. Called only after a check has already failed.
    /// </summary>
    private static string BuildMessage(string guardName, string? expression, string? detail = null)
    {
        string text = string.IsNullOrEmpty(expression) ? UnknownExpression : expression;

        return detail is null
            ? $"Guard.{guardName} ({text}) failed."
            : $"Guard.{guardName} ({text}) failed: {detail}";
    }
}
