namespace SsalKit.Randomness.Tests;

public class RandomStateTests
{
    [Fact]
    public void ExportState_ThenFromState_ContinuesIdenticalSequence()
    {
        var original = new DeterministicRandom(12345UL);

        // Advance a bit before exporting, so we're not just re-testing the constructor.
        for (int i = 0; i < 5; i++)
        {
            original.NextUInt64();
        }

        RandomState state = original.ExportState();
        DeterministicRandom restored = DeterministicRandom.FromState(state);

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(original.NextUInt64(), restored.NextUInt64());
        }
    }

    [Fact]
    public void ToArray_ThenFromSpan_RoundTripsToEquivalentState()
    {
        var random = new DeterministicRandom(999UL);
        RandomState original = random.ExportState();

        ulong[] array = original.ToArray();
        RandomState roundTripped = RandomState.FromSpan(array);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void CopyTo_ThenFromSpan_RoundTripsToEquivalentState()
    {
        var random = new DeterministicRandom(7UL);
        RandomState original = random.ExportState();

        Span<ulong> buffer = stackalloc ulong[4];
        original.CopyTo(buffer);
        RandomState roundTripped = RandomState.FromSpan(buffer);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void ToArray_ReturnsWordsInS0S1S2S3Order()
    {
        var state = new RandomState(1UL, 2UL, 3UL, 4UL);

        Assert.Equal([1UL, 2UL, 3UL, 4UL], state.ToArray());
    }

    [Fact]
    public void CopyTo_DestinationTooShort_ThrowsArgumentException()
    {
        var state = new RandomState(1UL, 2UL, 3UL, 4UL);
        var destination = new ulong[3];

        Assert.Throws<ArgumentException>(() => state.CopyTo(destination));
    }

    [Fact]
    public void FromSpan_SourceTooShort_ThrowsArgumentException()
    {
        ulong[] tooShort = [1UL, 2UL, 3UL];

        Assert.Throws<ArgumentException>(() => RandomState.FromSpan(tooShort));
    }

    [Fact]
    public void FromSpan_AllZero_ThrowsArgumentException()
    {
        ulong[] allZero = [0UL, 0UL, 0UL, 0UL];

        Assert.Throws<ArgumentException>(() => RandomState.FromSpan(allZero));
    }

    [Fact]
    public void IsValid_AllZeroState_IsFalse()
    {
        var state = new RandomState(0UL, 0UL, 0UL, 0UL);

        Assert.False(state.IsValid);
    }

    [Theory]
    [InlineData(1UL, 0UL, 0UL, 0UL)]
    [InlineData(0UL, 1UL, 0UL, 0UL)]
    [InlineData(0UL, 0UL, 1UL, 0UL)]
    [InlineData(0UL, 0UL, 0UL, 1UL)]
    public void IsValid_AnyNonZeroWord_IsTrue(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        var state = new RandomState(s0, s1, s2, s3);

        Assert.True(state.IsValid);
    }

    [Fact]
    public void FromState_AllZeroState_ThrowsArgumentException()
    {
        var allZero = new RandomState(0UL, 0UL, 0UL, 0UL);

        Assert.Throws<ArgumentException>(() => DeterministicRandom.FromState(allZero));
    }
}
