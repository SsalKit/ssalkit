using SsalKit.Guard.IntegrationTests.TestDomain;

namespace SsalKit.Guard.IntegrationTests;

/// <summary>
/// The end-to-end shape the library exists to replace: the prototype's
/// <c>ExceptionFilterAttribute.MapToAppStatusCode</c> -- a hand-ordered <see langword="switch"/>
/// over exception types, extended by hand, kept in derived-before-base order by comment. The same
/// boundary, rewritten as catch → <c>TryMap</c> → respond, produces the same answers with no
/// ordering to maintain.
/// </summary>
/// <seealso cref="BoundaryFilter"/>
public class BoundaryFilterTests
{
    /// <summary>
    /// One table, every kind of registration the container holds: an exception that declares its own
    /// code, its derived sibling (the case the prototype needed a comment for), a BCL exception
    /// registered externally, a guard failure, and something nobody registered at all.
    /// </summary>
    private static readonly (Action Operation, GameStatusCode Expected)[] Boundaries =
    [
        (static () => throw GameErrors.NotFound("no such thing"), GameStatusCode.NotFound),
        (static () => throw GameErrors.UserNotFound("no such user"), GameStatusCode.UserNotFound),
        (static () => GameErrors.ThrowInvalidTeam("team is not valid"), GameStatusCode.InvalidTeam),
        (static () => throw new TimeoutException("roster service timed out"), GameStatusCode.ServerBusy),
        (static () => Guard.That(false), GameStatusCode.GuardViolation),
        (static () => throw new InvalidOperationException("nobody registered this"), GameStatusCode.Unspecified),
    ];

    [Fact]
    public void SuccessfulOperation_ProducesNoFailure()
    {
        Assert.Null(BoundaryFilter.Run(static () => { }));
    }

    [Fact]
    public void EveryFailingOperation_IsReportedAsItsRegisteredCode()
    {
        Assert.All(Boundaries, boundary =>
        {
            FailureResponse? failure = BoundaryFilter.Run(boundary.Operation);

            Assert.NotNull(failure);
            Assert.Equal(boundary.Expected, failure.Code);
        });
    }

    /// <summary>
    /// The message travels untouched alongside the code, so the transport gets a machine-readable
    /// code and a human-readable reason without the boundary composing either of them.
    /// </summary>
    [Fact]
    public void FailingOperation_PassesTheExceptionMessageThrough()
    {
        FailureResponse? failure = BoundaryFilter.Run(
            static () => throw GameErrors.UserNotFound("user 42 is gone"));

        Assert.NotNull(failure);
        Assert.Equal("user 42 is gone", failure.Message);
    }

    /// <summary>
    /// A domain failure is separable from everything else by the base type alone -- the second reason
    /// <c>ErrorCodedException</c> exists -- so a boundary can decide to treat unexpected exceptions
    /// differently without consulting the mapping table at all.
    /// </summary>
    [Fact]
    public void DomainFailuresAreDistinguishableByTheirBaseType()
    {
        Exception domainFailure = GameErrors.UserNotFound();
        Exception guardFailure = new GuardViolationException();
        Exception unexpected = new TimeoutException();

        Assert.True(domainFailure is ErrorCodedException);
        Assert.True(guardFailure is ErrorCodedException);
        Assert.False(unexpected is ErrorCodedException);
    }
}
