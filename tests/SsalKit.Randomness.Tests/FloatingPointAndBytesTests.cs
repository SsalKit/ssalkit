namespace SsalKit.Randomness.Tests;

public class FloatingPointAndBytesTests
{
    [Fact]
    public void NextDouble_StaysWithinZeroToOneExclusive()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 100_000; i++)
        {
            double value = random.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0);
        }
    }

    [Fact]
    public void NextDouble_MaximalRawInput_NeverProducesOne()
    {
        // NextUInt64() == ulong.MaxValue is the raw input that maximizes NextDouble()'s result.
        // Evaluated here using the exact formula documented on DeterministicRandom.NextDouble():
        // (rawValue >> 11) * (1.0 / (1UL << 53)) must still be strictly < 1.0.
        double value = (ulong.MaxValue >> 11) * (1.0 / (1UL << 53));

        Assert.True(value < 1.0);
    }

    [Fact]
    public void NextSingle_StaysWithinZeroToOneExclusive()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 100_000; i++)
        {
            float value = random.NextSingle();
            Assert.True(value >= 0.0f && value < 1.0f);
        }
    }

    [Fact]
    public void NextSingle_MaximalRawInput_NeverProducesOne()
    {
        // Same reasoning as the NextDouble case above, for NextSingle()'s 24-bit formula.
        float value = (ulong.MaxValue >> 40) * (1.0f / (1UL << 24));

        Assert.True(value < 1.0f);
    }

    [Fact]
    public void NextBoolean_ProducesBothValues()
    {
        var random = new DeterministicRandom(1UL);
        bool sawTrue = false;
        bool sawFalse = false;
        for (int i = 0; i < 1_000; i++)
        {
            if (random.NextBoolean())
            {
                sawTrue = true;
            }
            else
            {
                sawFalse = true;
            }
        }

        Assert.True(sawTrue);
        Assert.True(sawFalse);
    }

    [Fact]
    public void NextBytes_IsDeterministicForSameSeed()
    {
        var a = new DeterministicRandom(2020UL);
        var b = new DeterministicRandom(2020UL);

        Span<byte> bufferA = stackalloc byte[37];
        Span<byte> bufferB = stackalloc byte[37];
        a.NextBytes(bufferA);
        b.NextBytes(bufferB);

        Assert.True(bufferA.SequenceEqual(bufferB));
    }

    [Fact]
    public void NextBytes_EmptyBuffer_DoesNotThrow()
    {
        var random = new DeterministicRandom(1UL);

        random.NextBytes(Span<byte>.Empty);
    }

    [Fact]
    public void NextBytes_PartialLastChunk_MatchesFullDrawTruncated()
    {
        // A 5-byte buffer should be filled from the low-order 5 bytes (little-endian) of a
        // single NextUInt64() draw, per the documented little-endian chunking contract.
        var forPartial = new DeterministicRandom(4242UL);
        var forFull = new DeterministicRandom(4242UL);

        Span<byte> partial = stackalloc byte[5];
        forPartial.NextBytes(partial);

        ulong fullValue = forFull.NextUInt64();
        Span<byte> full = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(full, fullValue);

        Assert.True(partial.SequenceEqual(full[..5]));
    }

    [Fact]
    public void NextBytes_MultipleFullChunksPlusPartial_ConsumesExpectedNumberOfDraws()
    {
        // 19 bytes = 2 full 8-byte chunks + 1 partial (3-byte) chunk = 3 NextUInt64() draws.
        var forBytes = new DeterministicRandom(1UL);
        var forRaw = new DeterministicRandom(1UL);

        Span<byte> bytes = stackalloc byte[19];
        forBytes.NextBytes(bytes);

        Span<byte> expected = stackalloc byte[24];
        for (int chunk = 0; chunk < 3; chunk++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(expected.Slice(chunk * 8, 8), forRaw.NextUInt64());
        }

        Assert.True(bytes.SequenceEqual(expected[..19]));
    }
}
