using SsalKit.Guard.IntegrationTests.TestDomain;

namespace SsalKit.Guard.IntegrationTests;

/// <summary>
/// The two halves of the library meeting: a <see cref="Guard"/> clause fails, the failure travels as
/// an exception, and the generated container turns it back into a code. Neither half knows about the
/// other -- the join is one <c>[ExternalErrorCode]</c> line on the container.
/// </summary>
public class GuardMappingTests
{
    /// <summary>
    /// The round trip the design calls for in place of the prototype's built-in
    /// "SystemIntegrityViolation" default: a guard failure is a domain failure like any other, and
    /// the consumer picks which of its own codes it is.
    /// </summary>
    [Fact]
    public void FailedGuard_MapsToTheCodeTheContainerRegisteredForIt()
    {
        int level = 3;

        var thrown = Assert.Throws<GuardViolationException>(() => Guard.That(level >= 10));

        Assert.True(GameErrors.TryMap(thrown, out GameStatusCode code));
        Assert.Equal(GameStatusCode.GuardViolation, code);
    }

    /// <summary>
    /// The expression text survives the trip, which is the point of the
    /// <c>[CallerArgumentExpression]</c> capture: the boundary gets a code for the transport and a
    /// message that still says which invariant broke.
    /// </summary>
    [Fact]
    public void FailedGuard_CarriesTheCapturedExpressionTextIntoTheMappedFailure()
    {
        int level = 3;

        FailureResponse? failure = BoundaryFilter.Run(() => Guard.That(level >= 10));

        Assert.NotNull(failure);
        Assert.Equal(GameStatusCode.GuardViolation, failure.Code);
        Assert.Equal("Guard.That (level >= 10) failed.", failure.Message);
    }

    /// <summary>
    /// Every guard clause funnels into the same registration -- the mapping is on the exception type,
    /// so it covers clauses the container has never heard of.
    /// </summary>
    [Fact]
    public void EveryGuardClause_MapsThroughTheSameRegistration()
    {
        var failures = new List<Action>
        {
            static () => Guard.That(false),
            static () => Guard.NotNull<string>(null),
            static () => Guard.NotNullOrEmpty(string.Empty),
            static () => Guard.NotNullOrWhiteSpace("   "),
            static () => Guard.InRange(11, 1, 10),
        };

        Assert.All(failures, failing =>
        {
            FailureResponse? failure = BoundaryFilter.Run(failing);

            Assert.NotNull(failure);
            Assert.Equal(GameStatusCode.GuardViolation, failure.Code);
        });
    }

    /// <summary>
    /// The custom-exception overload is how a guard reaches a <em>specific</em> code: the factory
    /// hands back a decorated exception, and the container maps it as it would any other.
    /// </summary>
    [Fact]
    public void GuardWithACustomFactory_ThrowsAnErrorCodedException_ThatMapsToItsOwnCode()
    {
        string? userId = null;

        var thrown = Assert.Throws<UserNotFoundException>(() =>
        {
            Guard.NotNull(userId, static () => GameErrors.UserNotFound("no user on the request"));
        });

        Assert.Equal("no user on the request", thrown.Message);
        Assert.True(GameErrors.TryMap(thrown, out GameStatusCode code));
        Assert.Equal(GameStatusCode.UserNotFound, code);
    }

    /// <summary>
    /// And the factory really is deferred: a passing guard never builds the exception, so the code
    /// above costs nothing on the success path.
    /// </summary>
    [Fact]
    public void GuardWithACustomFactory_DoesNotInvokeItOnTheSuccessPath()
    {
        int invocations = 0;

        Guard.That(true, () =>
        {
            invocations++;
            return GameErrors.UserNotFound();
        });

        Assert.Equal(0, invocations);
    }
}
