using SsalKit.Guard.IntegrationTests.TestDomain;

namespace SsalKit.Guard.IntegrationTests;

/// <summary>
/// The generated <c>TryMap</c>/<c>MapOrDefault</c> pair, exercised against the real generator output
/// rather than a snapshot: what the mapping table does at run time is the contract, and the
/// derived-before-base ordering is the whole reason the table is generated at all.
/// </summary>
public class MappingTests
{
    /// <summary>
    /// The registration in the container that this test would break if the ordering were lost:
    /// <see cref="UserNotFoundException"/> derives from <see cref="NotFoundException"/>, and both
    /// carry a code. Written base-first anywhere in the source, an <c>is</c> chain in declaration
    /// order would answer <see cref="GameStatusCode.NotFound"/> for both.
    /// </summary>
    [Fact]
    public void DerivedException_MapsToItsOwnCode_NotItsBaseTypes()
    {
        Assert.True(GameErrors.TryMap(new UserNotFoundException("no such user"), out GameStatusCode code));

        Assert.Equal(GameStatusCode.UserNotFound, code);
    }

    [Fact]
    public void BaseException_StillMapsToTheBaseCode()
    {
        Assert.True(GameErrors.TryMap(new NotFoundException("no such thing"), out GameStatusCode code));

        Assert.Equal(GameStatusCode.NotFound, code);
    }

    /// <summary>
    /// The match is on the runtime type, so where the instance is held makes no difference: a
    /// derived exception in a base-typed variable -- which is how one actually arrives at a
    /// <c>catch (Exception)</c> -- still maps to the derived code.
    /// </summary>
    [Fact]
    public void DerivedInstanceHeldInABaseTypedVariable_StillMapsToTheDerivedCode()
    {
        NotFoundException asBase = new UserNotFoundException("no such user");
        Exception asException = asBase;

        Assert.True(GameErrors.TryMap(asBase, out GameStatusCode fromBaseTyped));
        Assert.True(GameErrors.TryMap(asException, out GameStatusCode fromExceptionTyped));

        Assert.Equal(GameStatusCode.UserNotFound, fromBaseTyped);
        Assert.Equal(GameStatusCode.UserNotFound, fromExceptionTyped);
    }

    [Fact]
    public void ExternallyRegisteredException_Maps()
    {
        Assert.True(GameErrors.TryMap(new TimeoutException("gateway timed out"), out GameStatusCode code));

        Assert.Equal(GameStatusCode.ServerBusy, code);
    }

    [Fact]
    public void UnregisteredException_YieldsFalseAndTheDefaultCode()
    {
        Assert.False(GameErrors.TryMap(new InvalidOperationException("not ours"), out GameStatusCode code));

        Assert.Equal(default, code);
    }

    /// <summary>
    /// The documented null behaviour: <c>is</c> never matches a null reference, so a null exception
    /// is simply unmapped rather than a <see cref="NullReferenceException"/> thrown from inside the
    /// generated code.
    /// </summary>
    [Fact]
    public void NullException_IsUnmapped()
    {
        Assert.False(GameErrors.TryMap(null!, out GameStatusCode code));

        Assert.Equal(default, code);
    }

    [Fact]
    public void MapOrDefault_ReturnsTheRegisteredCode_WhenOneMatches()
    {
        GameStatusCode code = GameErrors.MapOrDefault(
            new UserNotFoundException("no such user"), GameStatusCode.Unspecified);

        Assert.Equal(GameStatusCode.UserNotFound, code);
    }

    [Fact]
    public void MapOrDefault_ReturnsTheFallback_WhenNothingMatches()
    {
        GameStatusCode code = GameErrors.MapOrDefault(
            new InvalidOperationException("not ours"), GameStatusCode.Unspecified);

        Assert.Equal(GameStatusCode.Unspecified, code);
    }

    /// <summary>
    /// The fallback is returned verbatim, including when it is a perfectly real code -- the caller
    /// decides what "unmapped" should look like, which is also why <c>TryMap</c> exists next to this.
    /// </summary>
    [Fact]
    public void MapOrDefault_DoesNotTreatTheFallbackSpecially()
    {
        GameStatusCode code = GameErrors.MapOrDefault(
            new InvalidOperationException("not ours"), GameStatusCode.ServerBusy);

        Assert.Equal(GameStatusCode.ServerBusy, code);
    }
}
