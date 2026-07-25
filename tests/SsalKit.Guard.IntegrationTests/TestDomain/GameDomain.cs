namespace SsalKit.Guard.IntegrationTests.TestDomain;

/// <summary>
/// The error codes of the pretend game domain these tests map onto. Deliberately an ordinary
/// <see langword="enum"/> with hand-picked numbers, as a real transport-facing code set would be.
/// </summary>
public enum GameStatusCode
{
    /// <summary>No code -- the fallback every unmapped exception is reported as.</summary>
    Unspecified = 0,

    /// <summary>Something was not found. The <em>base</em> of the inheritance pair below.</summary>
    NotFound = 1000,

    /// <summary>A user in particular was not found. The <em>derived</em> half of the pair.</summary>
    UserNotFound = 1001,

    /// <summary>A team composition rule was broken.</summary>
    InvalidTeam = 1002,

    /// <summary>A downstream dependency did not answer in time.</summary>
    ServerBusy = 2001,

    /// <summary>An internal invariant was violated -- what a failed <see cref="Guard"/> maps to.</summary>
    GuardViolation = 9001,
}

/// <summary>
/// The base of the inheritance pair. Not sealed, and registered with its own code, so that
/// <see cref="UserNotFoundException"/> below can derive from it and claim a different one.
/// </summary>
[ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
public class NotFoundException : ErrorCodedException
{
    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public NotFoundException(string? message = null)
        : base(message)
    {
    }
}

/// <summary>
/// The derived half of the pair, carrying a code of its own. In the prototype this is exactly the
/// case that needed a hand-written "must be matched first" comment above the mapping switch.
/// </summary>
[ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
public sealed class UserNotFoundException : NotFoundException
{
    /// <summary>Initializes a new instance of the <see cref="UserNotFoundException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public UserNotFoundException(string? message = null)
        : base(message)
    {
    }
}

/// <summary>
/// Declares the <c>(string?, Exception?)</c> constructor shape, so the generated helpers for it
/// mirror both parameters rather than only the message.
/// </summary>
[ErrorCode<GameStatusCode>(GameStatusCode.InvalidTeam)]
public sealed class InvalidTeamException : ErrorCodedException
{
    /// <summary>Initializes a new instance of the <see cref="InvalidTeamException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public InvalidTeamException(string? message = null, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The game domain's mapping container.
/// </summary>
/// <remarks>
/// <para>
/// The two <c>[ExternalErrorCode]</c> registrations are the two kinds of type that cannot carry
/// <c>[ErrorCode]</c> themselves: a BCL exception (<see cref="TimeoutException"/>, standing in for
/// the cache/cluster/token exceptions that made up half of the prototype's switch), and
/// <see cref="GuardViolationException"/>, which the library owns -- the pattern the design
/// recommends for giving guard failures a code of the consumer's own choosing.
/// </para>
/// <para>
/// Everything else about the container is generated: this file declares no mapping and no helper.
/// </para>
/// </remarks>
[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
public static partial class GameErrors
{
}
