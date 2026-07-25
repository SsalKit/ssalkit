using System.Reflection;
using SsalKit.Guard.IntegrationTests.TestDomain;

namespace SsalKit.Guard.IntegrationTests;

/// <summary>
/// Two containers over two code enums, in one assembly: each sees only the registrations written for
/// its own <c>TCode</c>, which is what makes per-domain code sets possible instead of one
/// assembly-wide enum everybody has to extend.
/// </summary>
public class MultipleContainerTests
{
    [Fact]
    public void EachContainer_MapsItsOwnExceptions()
    {
        Assert.True(GameErrors.TryMap(new UserNotFoundException(), out GameStatusCode gameCode));
        Assert.True(BillingErrors.TryMap(new CardDeclinedException(), out BillingStatusCode billingCode));

        Assert.Equal(GameStatusCode.UserNotFound, gameCode);
        Assert.Equal(BillingStatusCode.CardDeclined, billingCode);
    }

    [Fact]
    public void NeitherContainer_MapsTheOthersExceptions()
    {
        Assert.False(GameErrors.TryMap(new CardDeclinedException(), out _));
        Assert.False(BillingErrors.TryMap(new UserNotFoundException(), out _));
    }

    /// <summary>
    /// The same external type registered in both containers gets each one's own code -- the mapping
    /// belongs to the boundary, not to the exception type.
    /// </summary>
    [Fact]
    public void TheSameExternalException_MapsDifferentlyInEachContainer()
    {
        var timeout = new TimeoutException("timed out");

        Assert.True(GameErrors.TryMap(timeout, out GameStatusCode gameCode));
        Assert.True(BillingErrors.TryMap(timeout, out BillingStatusCode billingCode));

        Assert.Equal(GameStatusCode.ServerBusy, gameCode);
        Assert.Equal(BillingStatusCode.PaymentTimeout, billingCode);
    }

    /// <summary>
    /// The helpers are per container too: <c>BillingErrors</c> knows how to build its own exception
    /// and has never heard of the game domain's.
    /// </summary>
    [Fact]
    public void HelpersAreScopedToTheirOwnContainer()
    {
        CardDeclinedException declined = BillingErrors.CardDeclined("card was refused");

        Assert.Equal("card was refused", declined.Message);
        Assert.Null(typeof(BillingErrors).GetMethod("UserNotFound", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(GameErrors).GetMethod("CardDeclined", BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>
    /// The generated <c>TryMap</c> is typed on the container's own code enum, so a call site cannot
    /// silently cross domains: there is no overload that takes a <see cref="GameStatusCode"/> out of
    /// <see cref="BillingErrors"/>.
    /// </summary>
    [Fact]
    public void MappingSignaturesAreTypedOnTheirOwnCodeEnum()
    {
        MethodInfo? gameTryMap = typeof(GameErrors).GetMethod("TryMap", BindingFlags.Public | BindingFlags.Static);
        MethodInfo? billingTryMap = typeof(BillingErrors).GetMethod("TryMap", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(gameTryMap);
        Assert.NotNull(billingTryMap);
        Assert.Equal(typeof(GameStatusCode).MakeByRefType(), gameTryMap.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(BillingStatusCode).MakeByRefType(), billingTryMap.GetParameters()[1].ParameterType);
    }
}
