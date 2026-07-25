namespace SsalKit.Guard.IntegrationTests.TestDomain;

/// <summary>
/// What a caught exception is turned into at a service boundary: a code plus the message.
/// </summary>
/// <param name="Code">The mapped error code.</param>
/// <param name="Message">The exception's message, passed through untouched.</param>
public sealed record FailureResponse(GameStatusCode Code, string Message);

/// <summary>
/// The consumer-side half of the pattern, written the way a real boundary would: catch, map, respond.
/// </summary>
/// <remarks>
/// <para>
/// This is the direct replacement for the prototype's <c>MapToAppStatusCode</c> -- a hand-written
/// <see langword="switch"/> whose arms had to be kept in derived-before-base order, guarded by a
/// comment saying so, and extended by hand every time a new exception type appeared. Here the
/// ordering is the generated <c>TryMap</c>'s problem, and a new exception type joins the table by
/// carrying <c>[ErrorCode]</c>.
/// </para>
/// <para>
/// Note what this class does <em>not</em> do: nothing tags an <c>Activity</c>, writes a log, or
/// touches any ambient state. The exceptions themselves are pure data (the prototype's constructors
/// tagged <c>Activity.Current</c>, which is the side effect this design removes), and observability
/// is the boundary's business -- it would go here, next to the mapping, where the request context
/// actually exists.
/// </para>
/// </remarks>
public static class BoundaryFilter
{
    /// <summary>
    /// Runs <paramref name="operation"/> and reports its failure as an error code, or
    /// <see langword="null"/> when it succeeded.
    /// </summary>
    /// <param name="operation">The domain operation to run.</param>
    /// <returns>
    /// <see langword="null"/> on success; otherwise the mapped failure, with
    /// <see cref="GameStatusCode.Unspecified"/> when nothing in the container matched.
    /// </returns>
    public static FailureResponse? Run(Action operation)
    {
        try
        {
            operation();
            return null;
        }
        catch (Exception exception)
        {
            // TryMap rather than MapOrDefault, because "no registration matched" is a different
            // event from any real code: it is the one case a boundary wants to log loudly.
            return GameErrors.TryMap(exception, out GameStatusCode code)
                ? new FailureResponse(code, exception.Message)
                : new FailureResponse(GameStatusCode.Unspecified, exception.Message);
        }
    }
}
