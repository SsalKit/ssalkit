namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §6 test 3 / §6 test 6: the equality-consistency invariant (§4.2 -- "for every
/// supported type, <c>a == b</c> implies <c>encode(a) == encode(b)</c>") verified end to end
/// through the real generated <c>ComputeStableHash()</c> extension, not just through direct
/// <see cref="StableHashWriter"/> calls (which
/// SsalKit.StableHashing.Tests.EqualityConsistencyTests already covers at the writer layer).
/// </summary>
public class EqualityConsistencyIntegrationTests
{
    [Fact]
    public void DecimalMember_TrailingZeroVariant_ProducesSameHashAsBaseline()
    {
        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { Decimal = 1.0m };
        ComprehensiveContract variant = TestFixtures.BuildComprehensiveInstance() with { Decimal = 1.00m };
        Assert.Equal(baseline.Decimal, variant.Decimal);

        Assert.Equal(baseline.ComputeStableHash(), variant.ComputeStableHash());
    }

    [Fact]
    public void DateTimeOffsetMember_SameInstantDifferentOffset_ProducesSameHashAsBaseline()
    {
        var utc = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var plusOne = new DateTimeOffset(2026, 7, 30, 13, 0, 0, TimeSpan.FromHours(1));
        Assert.Equal(utc, plusOne);

        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { DateTimeOffset = utc };
        ComprehensiveContract variant = TestFixtures.BuildComprehensiveInstance() with { DateTimeOffset = plusOne };

        Assert.Equal(baseline.ComputeStableHash(), variant.ComputeStableHash());
    }

    [Fact]
    public void SingleMember_NegativeAndPositiveZero_ProduceSameHash()
    {
        Assert.Equal(-0f, 0f);

        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { Single = 0f };
        ComprehensiveContract variant = TestFixtures.BuildComprehensiveInstance() with { Single = -0f };

        Assert.Equal(baseline.ComputeStableHash(), variant.ComputeStableHash());
    }

    [Fact]
    public void DoubleMember_NegativeAndPositiveZero_ProduceSameHash()
    {
        Assert.Equal(-0d, 0d);

        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { Double = 0d };
        ComprehensiveContract variant = TestFixtures.BuildComprehensiveInstance() with { Double = -0d };

        Assert.Equal(baseline.ComputeStableHash(), variant.ComputeStableHash());
    }

    [Fact]
    public void DoubleMember_DifferentNaNBitPatterns_ProduceSameHash()
    {
        double quietNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000L);
        double signalingNaN = BitConverter.Int64BitsToDouble(0x7FF4000000000001L);
        Assert.True(double.IsNaN(quietNaN));
        Assert.True(double.IsNaN(signalingNaN));
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(quietNaN), BitConverter.DoubleToInt64Bits(signalingNaN));

        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { Double = quietNaN };
        ComprehensiveContract variant = TestFixtures.BuildComprehensiveInstance() with { Double = signalingNaN };

        Assert.Equal(baseline.ComputeStableHash(), variant.ComputeStableHash());
    }

    [Fact]
    public void SingleMember_DifferentNaNBitPatterns_ProduceSameHash()
    {
        float quietNaN = BitConverter.Int32BitsToSingle(unchecked((int)0x7FC00000));
        float negativeNaN = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC12345));
        Assert.True(float.IsNaN(quietNaN));
        Assert.True(float.IsNaN(negativeNaN));

        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { Single = quietNaN };
        ComprehensiveContract variant = TestFixtures.BuildComprehensiveInstance() with { Single = negativeNaN };

        Assert.Equal(baseline.ComputeStableHash(), variant.ComputeStableHash());
    }
}
