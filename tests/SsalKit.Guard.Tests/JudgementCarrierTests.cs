using System.Reflection;

namespace SsalKit.Guard.Tests;

/// <summary>
/// The carriers the factories hand back, and the conversions that turn them into judgements —
/// the machinery that keeps type arguments off the call site.
/// </summary>
public sealed class JudgementCarrierTests
{
    [Fact]
    public void Grant_RoundTripsToAPayloadFreeJudgement()
    {
        Judgement<ShopCode> verdict = Judgement.Grant();

        Assert.True(verdict.IsGranted);
        Assert.Null(verdict.RejectedWith);
    }

    [Fact]
    public void GrantWithState_RoundTripsToAPayloadCarryingJudgement()
    {
        Judgement<Inventory, ShopCode> purchase = Judgement.Grant(new Inventory(90));

        Assert.True(purchase.IsGranted);
        Assert.Equal(new Inventory(90), purchase.Granted);
    }

    [Fact]
    public void Reject_RoundTripsToAPayloadFreeJudgement()
    {
        Judgement<ShopCode> verdict = Judgement.Reject(ShopCode.Banned, "Trading is suspended.");

        Assert.Equal(ShopCode.Banned, verdict.RejectedWith);
        Assert.Equal("Trading is suspended.", verdict.RejectionMessage);
    }

    [Fact]
    public void Reject_RoundTripsToAPayloadCarryingJudgement()
    {
        Judgement<Inventory, ShopCode> purchase =
            Judgement.Reject(ShopCode.Banned, "Trading is suspended.");

        Assert.Null(purchase.Granted);
        Assert.Equal(ShopCode.Banned, purchase.RejectedWith);
        Assert.Equal("Trading is suspended.", purchase.RejectionMessage);
    }

    /// <summary>
    /// A rejection carries no state, so one carrier fits either shape. This is what lets the
    /// refusing branch of a rule be written without naming the payload type.
    /// </summary>
    [Fact]
    public void OneRejectionCarrier_ConvertsToEitherShape()
    {
        var carrier = Judgement.Reject(ShopCode.NotSold, "gone");

        Judgement<ShopCode> verdict = carrier;
        Judgement<Inventory, ShopCode> purchase = carrier;

        Assert.Equal(ShopCode.NotSold, verdict.RejectedWith);
        Assert.Equal(ShopCode.NotSold, purchase.RejectedWith);
    }

    [Fact]
    public void Grant_WithNullState_IsRejectedByTheFactory()
    {
        var exception = Assert.Throws<ArgumentNullException>(static () =>
        {
            _ = Judgement.Grant<Inventory>(null!);
        });

        Assert.Equal("granted", exception.ParamName);
    }

    [Fact]
    public void Reject_WithANullMessage_IsRejectedByTheFactory()
    {
        var exception = Assert.Throws<ArgumentNullException>(static () =>
        {
            _ = Judgement.Reject(ShopCode.NotSold, null!);
        });

        Assert.Equal("message", exception.ParamName);
    }

    /// <summary>
    /// The empty message is a legal rejection, and must not be mistaken for a grant.
    /// </summary>
    [Fact]
    public void Reject_WithAnEmptyMessage_IsStillARejection()
    {
        Judgement<ShopCode> verdict = Judgement.Reject(ShopCode.NotSold, string.Empty);
        Judgement<Inventory, ShopCode> purchase = Judgement.Reject(ShopCode.NotSold, string.Empty);

        Assert.False(verdict.IsGranted);
        Assert.False(purchase.IsGranted);
        Assert.Equal(string.Empty, verdict.RejectionMessage);
        Assert.Equal(string.Empty, purchase.RejectionMessage);
    }

    /// <summary>
    /// The payload-free grant carrier holds nothing, so its default value is indistinguishable
    /// from a produced one — and means the same thing.
    /// </summary>
    [Fact]
    public void ADefaultStatelessGrantCarrier_IsStillAGrant()
    {
        Judgement<ShopCode> verdict = AsVerdict(default(GrantedJudgement));

        Assert.Same(Judgement<ShopCode>.Granted, verdict);
    }

    [Fact]
    public void ADefaultGrantCarrierWithState_CannotBecomeAJudgement()
    {
        var exception = Assert.Throws<InvalidOperationException>(static () =>
        {
            _ = AsPurchase(default(GrantedJudgement<Inventory>));
        });

        Assert.Contains("GrantedJudgement<Inventory>", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Judgement.Grant", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADefaultRejectionCarrier_CannotBecomeAPayloadFreeJudgement()
    {
        var exception = Assert.Throws<InvalidOperationException>(static () =>
        {
            _ = AsVerdict(default(RejectedJudgement<ShopCode>));
        });

        Assert.Contains("RejectedJudgement<ShopCode>", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Judgement.Reject", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADefaultRejectionCarrier_CannotBecomeAPayloadCarryingJudgement()
    {
        var exception = Assert.Throws<InvalidOperationException>(static () =>
        {
            _ = AsPurchase(default(RejectedJudgement<ShopCode>));
        });

        Assert.Contains("RejectedJudgement<ShopCode>", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Judgement.Reject", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both branches of a conditional expression convert to the return type, so a whole rule fits
    /// in one expression with no type argument in sight.
    /// </summary>
    [Fact]
    public void AConditionalExpression_ProducesEitherOutcome_PayloadFree()
    {
        var allowed = CanTrade(banned: false);
        var refused = CanTrade(banned: true);

        Assert.True(allowed.IsGranted);
        Assert.Equal(ShopCode.Banned, refused.RejectedWith);
        Assert.Equal("Trading is suspended.", refused.RejectionMessage);
    }

    [Fact]
    public void AConditionalExpression_ProducesEitherOutcome_PayloadCarrying()
    {
        var bought = Buy(new Inventory(90), price: 20);
        var refused = Buy(new Inventory(10), price: 20);

        Assert.Equal(new Inventory(70), bought.Granted);
        Assert.Null(refused.Granted);
        Assert.Equal(ShopCode.NotEnoughGold, refused.RejectedWith);
    }

    /// <summary>
    /// A domain whose refusals always use one code needs nothing from the library: a helper
    /// returning the carrier fits either judgement shape, exactly like the factory does.
    /// </summary>
    [Fact]
    public void AFixedCodeRejectionHelper_FitsBothShapes()
    {
        Judgement<ShopCode> verdict = ShopRejections.NotSold("The shop does not sell 7.");
        Judgement<Inventory, ShopCode> purchase = ShopRejections.NotSold("The shop does not sell 7.");

        Assert.Equal(ShopCode.NotSold, verdict.RejectedWith);
        Assert.Equal(ShopCode.NotSold, purchase.RejectedWith);
        Assert.Null(purchase.Granted);
    }

    /// <summary>
    /// Carriers are opaque by construction: everything they hold is internal, so the only thing a
    /// consumer can do with one is convert it. Nothing here is inherited — <c>Equals</c> and
    /// friends come from <see cref="ValueType"/> and are not declared on the carriers themselves.
    /// </summary>
    [Fact]
    public void Carriers_DeclareNoPublicMembers()
    {
        Assert.Empty(DeclaredPublicMembersOf(typeof(GrantedJudgement)));
        Assert.Empty(DeclaredPublicMembersOf(typeof(GrantedJudgement<Inventory>)));
        Assert.Empty(DeclaredPublicMembersOf(typeof(RejectedJudgement<ShopCode>)));
    }

    private static MemberInfo[] DeclaredPublicMembersOf(Type type)
        => type.GetMembers(
            BindingFlags.Public
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly);

    private static Judgement<ShopCode> AsVerdict(GrantedJudgement carrier) => carrier;

    private static Judgement<ShopCode> AsVerdict(RejectedJudgement<ShopCode> carrier) => carrier;

    private static Judgement<Inventory, ShopCode> AsPurchase(GrantedJudgement<Inventory> carrier)
        => carrier;

    private static Judgement<Inventory, ShopCode> AsPurchase(RejectedJudgement<ShopCode> carrier)
        => carrier;

    private static Judgement<ShopCode> CanTrade(bool banned)
        => banned
            ? Judgement.Reject(ShopCode.Banned, "Trading is suspended.")
            : Judgement.Grant();

    private static Judgement<Inventory, ShopCode> Buy(Inventory inventory, int price)
        => inventory.Gold >= price
            ? Judgement.Grant(new Inventory(inventory.Gold - price))
            : Judgement.Reject(ShopCode.NotEnoughGold, "Not enough gold.");

    private static class ShopRejections
    {
        public static RejectedJudgement<ShopCode> NotSold(string message)
            => Judgement.Reject(ShopCode.NotSold, message);
    }
}
