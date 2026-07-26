using System.Text.Json;

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

        Assert.False(state.IsValid());
    }

    [Theory]
    [InlineData(1UL, 0UL, 0UL, 0UL)]
    [InlineData(0UL, 1UL, 0UL, 0UL)]
    [InlineData(0UL, 0UL, 1UL, 0UL)]
    [InlineData(0UL, 0UL, 0UL, 1UL)]
    public void IsValid_AnyNonZeroWord_IsTrue(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        var state = new RandomState(s0, s1, s2, s3);

        Assert.True(state.IsValid());
    }

    [Fact]
    public void FromState_AllZeroState_ThrowsArgumentException()
    {
        var allZero = new RandomState(0UL, 0UL, 0UL, 0UL);

        Assert.Throws<ArgumentException>(() => DeterministicRandom.FromState(allZero));
    }

    // ---- System.Text.Json round-trip (the "trivially JSON-serializable" claim, asserted) ----

    [Fact]
    public void SystemTextJson_RoundTripsToAnEqualState_WithNoConverter()
    {
        var random = new DeterministicRandom(4242UL);
        RandomState original = random.ExportState();

        string json = JsonSerializer.Serialize(original);
        RandomState roundTripped = JsonSerializer.Deserialize<RandomState>(json);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void SystemTextJson_RoundTrippedState_ResumesTheIdenticalSequence()
    {
        var original = new DeterministicRandom(20260726UL);
        for (int i = 0; i < 5; i++)
        {
            original.NextUInt64();
        }

        string json = JsonSerializer.Serialize(original.ExportState());
        DeterministicRandom restored = DeterministicRandom.FromState(JsonSerializer.Deserialize<RandomState>(json));

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(original.NextUInt64(), restored.NextUInt64());
        }
    }

    [Fact]
    public void SystemTextJson_WordsAboveTwoToThe53_SurviveExactly()
    {
        // The documented caveat is about JavaScript consumers, not about .NET: System.Text.Json
        // reads these back into ulong bit-for-bit, including ulong.MaxValue and values whose
        // nearest double is a different integer. A JS `number` would not.
        var state = new RandomState(ulong.MaxValue, (1UL << 53) + 1UL, 0x8000_0000_0000_0001UL, 12_345_678_901_234_567UL);

        RandomState roundTripped = JsonSerializer.Deserialize<RandomState>(JsonSerializer.Serialize(state));

        Assert.Equal(state, roundTripped);
        Assert.NotEqual((ulong)(double)state.S1, state.S1);
    }

    [Fact]
    public void SystemTextJson_SerializesTheFourWordsAsJsonNumbers()
    {
        // The four state words are written as plain JSON numbers -- which is exactly why the
        // JavaScript 2^53 caveat applies to them. IsValid is a method precisely so that no
        // serializer adds a derived fifth field; pinning the whole document here means a change
        // to that shape cannot slip out unnoticed, since the payload is something consumers
        // persist.
        string json = JsonSerializer.Serialize(new RandomState(1UL, 2UL, 3UL, 4UL));

        Assert.Equal("""{"S0":1,"S1":2,"S2":3,"S3":4}""", json);
    }

    [Fact]
    public void SystemTextJson_UnknownExtraField_IsIgnoredWhenReadingBack()
    {
        // A payload written by an older build (which serialized a derived IsValid property) or by
        // a hand-rolled producer still round-trips: unknown members are skipped by default.
        RandomState state = JsonSerializer.Deserialize<RandomState>("""{"S0":1,"S1":2,"S2":3,"S3":4,"IsValid":false}""");

        Assert.Equal(new RandomState(1UL, 2UL, 3UL, 4UL), state);
        Assert.True(state.IsValid());
    }
}
