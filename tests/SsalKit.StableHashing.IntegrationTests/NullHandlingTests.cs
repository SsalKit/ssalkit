namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §3.4 / §6 test 6: a <see langword="null"/> argument to a class contract's generated
/// <c>ComputeStableHash()</c> throws <see cref="ArgumentNullException"/>; a nullable member's
/// <see langword="null"/> vs. present-value states must be distinguishable in the resulting hash.
/// </summary>
public class NullHandlingTests
{
    [Fact]
    public void ClassContract_NullValue_ThrowsArgumentNullException()
    {
        ComprehensiveContract? value = null;

        var exception = Assert.Throws<ArgumentNullException>(() => value!.ComputeStableHash());
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RecordClassContract_NullValue_ThrowsArgumentNullException()
    {
        PlayerName? value = null;

        var exception = Assert.Throws<ArgumentNullException>(() => value!.ComputeStableHash());
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void NullableValueMember_NullVsPresentValue_ProducesDifferentHash()
    {
        ComprehensiveContract withNull = TestFixtures.BuildComprehensiveInstance() with { NullableInt = null };
        ComprehensiveContract withValue = TestFixtures.BuildComprehensiveInstance() with { NullableInt = 0 };

        Assert.NotEqual(withNull.ComputeStableHash(), withValue.ComputeStableHash());
    }

    [Fact]
    public void NullableReferenceMember_NullVsEmptyString_ProducesDifferentHash()
    {
        ComprehensiveContract withNull = TestFixtures.BuildComprehensiveInstance() with { NullableString = null };
        ComprehensiveContract withEmpty = TestFixtures.BuildComprehensiveInstance() with { NullableString = "" };

        Assert.NotEqual(withNull.ComputeStableHash(), withEmpty.ComputeStableHash());
    }

    [Fact]
    public void RequiredStringMember_EmptyVsNonEmpty_ProducesDifferentHash()
    {
        ComprehensiveContract empty = TestFixtures.BuildComprehensiveInstance() with { String = "" };
        ComprehensiveContract nonEmpty = TestFixtures.BuildComprehensiveInstance() with { String = "x" };

        Assert.NotEqual(empty.ComputeStableHash(), nonEmpty.ComputeStableHash());
    }
}
