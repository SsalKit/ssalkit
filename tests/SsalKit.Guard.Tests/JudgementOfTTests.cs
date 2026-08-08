namespace SsalKit.Guard.Tests;

/// <summary>
/// <c>Judgement&lt;T, TCode&gt;</c> — the half that carries the new state, and with it the
/// nullable-reference narrowing that makes a missed rejection check a compile error.
/// </summary>
public sealed class JudgementOfTTests
{
    [Fact]
    public void Grant_CarriesTheNewStateAndNothingElse()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));

        Assert.True(judgement.IsGranted);
        Assert.Equal(new Inventory(90), judgement.Granted);
        Assert.Null(judgement.RejectedWith);
        Assert.Equal(string.Empty, judgement.RejectionMessage);
    }

    [Fact]
    public void Reject_CarriesTheCodeAndTheMessageAndNoNewState()
    {
        Judgement<Inventory, ShopCode> judgement =
            Judgement.Reject(ShopCode.NotEnoughGold, "100 gold needed.");

        Assert.False(judgement.IsGranted);
        Assert.Null(judgement.Granted);
        Assert.Equal(ShopCode.NotEnoughGold, judgement.RejectedWith);
        Assert.Equal("100 gold needed.", judgement.RejectionMessage);
    }

    [Fact]
    public void TryGetRejection_OnARejection_IsTrueAndHandsBackBoth()
    {
        Judgement<Inventory, ShopCode> judgement =
            Judgement.Reject(ShopCode.NotEnoughGold, "100 gold needed.");

        bool rejected = judgement.TryGetRejection(out var code, out var message);

        Assert.True(rejected);
        Assert.Equal(ShopCode.NotEnoughGold, code);
        Assert.Equal("100 gold needed.", message);
    }

    [Fact]
    public void TryGetRejection_OnAGrant_IsFalseAndFillsTheOutputsWithEmptyValues()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));

        bool rejected = judgement.TryGetRejection(out var code, out var message);

        Assert.False(rejected);
        Assert.Equal(default, code);
        Assert.Equal(string.Empty, message);
    }

    /// <summary>
    /// The point of <c>[MemberNotNullWhen(false, nameof(Granted))]</c>: after the rejection branch
    /// has been ruled out, the new state is reached with no <c>!</c> and no second null test. This
    /// project compiles with nullable warnings as errors, so the assertion is the compilation.
    /// </summary>
    [Fact]
    public void TryGetRejection_FalseBranch_ReachesTheNewStateWithoutASuppression()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));

        if (!judgement.TryGetRejection(out _, out _))
        {
            Inventory inventory = judgement.Granted;

            Assert.Equal(90, inventory.Gold);
            return;
        }

        Assert.Fail("A granted judgement must not report a rejection.");
    }

    /// <summary>
    /// The same for <c>[MemberNotNullWhen(true, nameof(Granted))]</c> on <c>IsGranted</c>.
    /// </summary>
    [Fact]
    public void IsGranted_TrueBranch_ReachesTheNewStateWithoutASuppression()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));

        if (judgement.IsGranted)
        {
            Inventory inventory = judgement.Granted;

            Assert.Equal(90, inventory.Gold);
            return;
        }

        Assert.Fail("A granted judgement must report IsGranted.");
    }

    [Fact]
    public void ToString_Granted_RendersTheNewState()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));

        Assert.Equal("Granted(Inventory(90 gold))", judgement.ToString());
    }

    [Fact]
    public void ToString_Rejected_NamesTheCodeAndKeepsTheMessage()
    {
        Judgement<Inventory, ShopCode> judgement =
            Judgement.Reject(ShopCode.NotEnoughGold, "100 gold needed.");

        Assert.Equal("Rejected(NotEnoughGold): 100 gold needed.", judgement.ToString());
    }

    [Fact]
    public void Grants_WithEqualPayloads_AreEqual()
    {
        Judgement<Inventory, ShopCode> left = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> right = Judgement.Grant(new Inventory(90));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Grants_WithDifferentPayloads_AreNotEqual()
    {
        Judgement<Inventory, ShopCode> left = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> right = Judgement.Grant(new Inventory(80));

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Rejections_WithTheSameCodeAndMessage_AreEqual()
    {
        Judgement<Inventory, ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<Inventory, ShopCode> right = Judgement.Reject(ShopCode.NotSold, "gone");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Rejections_DifferingOnlyInTheCode_AreNotEqual()
    {
        Judgement<Inventory, ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<Inventory, ShopCode> right = Judgement.Reject(ShopCode.NotEnoughGold, "gone");

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Rejections_DifferingOnlyInTheMessage_AreNotEqual()
    {
        Judgement<Inventory, ShopCode> left = Judgement.Reject(ShopCode.NotSold, "gone");
        Judgement<Inventory, ShopCode> right = Judgement.Reject(ShopCode.NotSold, "sold out");

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void AGrantAndARejection_AreNotEqual()
    {
        Judgement<Inventory, ShopCode> grant = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> rejection = Judgement.Reject(ShopCode.NotSold, "gone");

        Assert.NotEqual(grant, rejection);
    }

    [Fact]
    public void Equals_ComparesAgainstAnyObject()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> twin = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> different = Judgement.Grant(new Inventory(80));
        object same = twin;
        object other = different;

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
        Judgement<Inventory, ShopCode> boxedComparison = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> typedComparison = Judgement.Grant(new Inventory(90));

        bool equalToBoxedNull = boxedComparison.Equals((object?)null);
        bool equalToTypedNull = typedComparison.Equals(null);

        Assert.False(equalToBoxedNull);
        Assert.False(equalToTypedNull);
    }

    [Fact]
    public void EqualityOperators_AgreeWithValueEquality()
    {
        Judgement<Inventory, ShopCode> left = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> right = Judgement.Grant(new Inventory(90));
        Judgement<Inventory, ShopCode> rejection = Judgement.Reject(ShopCode.NotSold, "gone");

        bool equalPair = left == right;
        bool differentPair = left != right;
        bool equalMixed = left == rejection;
        bool differentMixed = left != rejection;

        Assert.True(equalPair);
        Assert.False(differentPair);
        Assert.False(equalMixed);
        Assert.True(differentMixed);
    }

    [Fact]
    public void With_CopiesTheJudgementUnchanged()
    {
        Judgement<Inventory, ShopCode> judgement = Judgement.Grant(new Inventory(90));

        var copy = judgement with { };

        Assert.Equal(judgement, copy);
        Assert.NotSame(judgement, copy);
    }
}
