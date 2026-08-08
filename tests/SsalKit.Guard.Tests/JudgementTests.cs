namespace SsalKit.Guard.Tests;

/// <summary>
/// The error-code vocabulary the judgement tests reject with. Shared by
/// <see cref="JudgementTests"/>, <see cref="JudgementOfTTests"/>, and
/// <see cref="JudgementCarrierTests"/>.
/// </summary>
public enum ShopCode
{
    None = 0,
    NotSold = 1,
    NotEnoughGold = 2,
    Banned = 3,
}

/// <summary>
/// The payload the judgement tests grant. A record, which is what the library documents as the way
/// to carry new state — and which gives the payload value equality, so the judgements built around
/// it are compared by value too. <see cref="ToString"/> is overridden to keep the rendering
/// assertions readable.
/// </summary>
public sealed record Inventory(int Gold)
{
    public override string ToString() => $"Inventory({Gold} gold)";
}

/// <summary>
/// <c>Judgement&lt;TCode&gt;</c> — the payload-free half of the family.
/// </summary>
public sealed class JudgementTests
{
    [Fact]
    public void Granted_HasNoCodeAndAnEmptyMessage()
    {
        var judgement = Judgement<ShopCode>.Granted;

        Assert.True(judgement.IsGranted);
        Assert.Null(judgement.RejectedWith);
        Assert.Equal(string.Empty, judgement.RejectionMessage);
    }

    [Fact]
    public void Granted_IsOneCachedInstancePerClosedCodeEnum()
    {
        Assert.Same(Judgement<ShopCode>.Granted, Judgement<ShopCode>.Granted);
    }

    [Fact]
    public void Grant_ConvertsToThatSameCachedInstance()
    {
        Judgement<ShopCode> judgement = Judgement.Grant();

        Assert.Same(Judgement<ShopCode>.Granted, judgement);
    }

    [Fact]
    public void Reject_CarriesTheCodeAndTheMessage()
    {
        Judgement<ShopCode> judgement = Judgement.Reject(ShopCode.Banned, "Trading is suspended.");

        Assert.False(judgement.IsGranted);
        Assert.Equal(ShopCode.Banned, judgement.RejectedWith);
        Assert.Equal("Trading is suspended.", judgement.RejectionMessage);
    }

    [Fact]
    public void TryGetRejection_OnARejection_IsTrueAndHandsBackBoth()
    {
        Judgement<ShopCode> judgement = Judgement.Reject(ShopCode.Banned, "Trading is suspended.");

        bool rejected = judgement.TryGetRejection(out var code, out var message);

        Assert.True(rejected);
        Assert.Equal(ShopCode.Banned, code);
        Assert.Equal("Trading is suspended.", message);
    }

    [Fact]
    public void TryGetRejection_OnAGrant_IsFalseAndFillsTheOutputsWithEmptyValues()
    {
        var judgement = Judgement<ShopCode>.Granted;

        bool rejected = judgement.TryGetRejection(out var code, out var message);

        Assert.False(rejected);
        Assert.Equal(default, code);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void ToString_Granted_IsJustThat()
    {
        Assert.Equal("Granted", Judgement<ShopCode>.Granted.ToString());
    }

    [Fact]
    public void ToString_Rejected_NamesTheCodeAndKeepsTheMessage()
    {
        Judgement<ShopCode> judgement = Judgement.Reject(ShopCode.NotSold, "The shop does not sell 7.");

        Assert.Equal("Rejected(NotSold): The shop does not sell 7.", judgement.ToString());
    }

    [Fact]
    public void Rejections_WithTheSameCodeAndMessage_AreEqual()
    {
        Judgement<ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<ShopCode> right = Judgement.Reject(ShopCode.NotSold, "gone");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Rejections_DifferingOnlyInTheCode_AreNotEqual()
    {
        Judgement<ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<ShopCode> right = Judgement.Reject(ShopCode.NotEnoughGold, "gone");

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Rejections_DifferingOnlyInTheMessage_AreNotEqual()
    {
        Judgement<ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<ShopCode> right = Judgement.Reject(ShopCode.NotSold, "sold out");

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void AGrantAndARejection_AreNotEqual()
    {
        Judgement<ShopCode> rejection = Judgement.Reject(ShopCode.NotSold, "gone");

        Assert.NotEqual(Judgement<ShopCode>.Granted, rejection);
    }

    [Fact]
    public void Equals_ComparesAgainstAnyObject()
    {
        Judgement<ShopCode> judgement = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<ShopCode> twin = Judgement.Reject(ShopCode.NotSold, "gone");
        object same = twin;
        object other = Judgement<ShopCode>.Granted;

        bool equalToSame = judgement.Equals(same);
        bool equalToOther = judgement.Equals(other);

        Assert.True(equalToSame);
        Assert.False(equalToOther);
    }

    [Fact]
    public void Equals_IsFalseForNull()
    {
        // Two locals rather than one: after 'x.Equals(null)' the compiler's flow analysis has
        // learned that x might be null, so a second call on the same local would not compile.
        Judgement<ShopCode> boxedComparison = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<ShopCode> typedComparison = Judgement.Reject(ShopCode.NotSold, "gone");

        bool equalToBoxedNull = boxedComparison.Equals((object?)null);
        bool equalToTypedNull = typedComparison.Equals(null);

        Assert.False(equalToBoxedNull);
        Assert.False(equalToTypedNull);
    }

    [Fact]
    public void EqualityOperators_AgreeWithValueEquality()
    {
        Judgement<ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<ShopCode> right = Judgement.Reject(ShopCode.NotSold, "gone");
        var grant = Judgement<ShopCode>.Granted;

        bool equalPair = left == right;
        bool differentPair = left != right;
        bool equalMixed = left == grant;
        bool differentMixed = left != grant;

        Assert.True(equalPair);
        Assert.False(differentPair);
        Assert.False(equalMixed);
        Assert.True(differentMixed);
    }

    /// <summary>
    /// A judgement is immutable and has no <c>init</c> setters, so a <c>with</c> expression can
    /// only ever produce a copy. It is legal, and this pins what it does.
    /// </summary>
    [Fact]
    public void With_CopiesTheJudgementUnchanged()
    {
        Judgement<ShopCode> judgement = Judgement.Reject(ShopCode.NotSold, "gone");

        var copy = judgement with { };

        Assert.Equal(judgement, copy);
        Assert.NotSame(judgement, copy);
    }
}
