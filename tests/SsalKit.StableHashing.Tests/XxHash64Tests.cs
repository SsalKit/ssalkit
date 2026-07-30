using SystemIoHashing = System.IO.Hashing;

namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Validates the internal <see cref="XxHash64"/> streaming port (design doc §6 test 1) two ways:
/// against well-known, independently published XXH64 (seed 0) test vectors, and against
/// <c>System.IO.Hashing.XxHash64</c> — a real, independently maintained implementation — used here
/// purely as a test-time oracle (it is not a dependency of the shipped SsalKit.StableHashing
/// package). Streaming-boundary tests confirm that chunking (including chunks that straddle the
/// internal 32-byte lane buffer) never changes the result.
/// </summary>
public class XxHash64Tests
{
    /// <summary>
    /// Widely published XXH64 (seed 0) test vectors — these exact values appear across multiple
    /// independent XXH64 implementations (e.g. the Go, Rust, and Python xxhash bindings) and were
    /// re-confirmed here against <c>System.IO.Hashing.XxHash64</c> while writing this test.
    /// </summary>
    public static TheoryData<byte[], ulong> WellKnownVectors => new()
    {
        { [], 0xEF46DB3751D8E999UL },
        { "a"u8.ToArray(), 0xD24EC4F1A98C6E5BUL },
        { "abc"u8.ToArray(), 0x44BC2CF5AD770999UL },
        { "123456789"u8.ToArray(), 0x8CB841DB40E6AE83UL },
    };

    [Theory]
    [MemberData(nameof(WellKnownVectors))]
    public void Digest_WellKnownVector_MatchesPublishedValue(byte[] input, ulong expected)
    {
        XxHash64 hasher = XxHash64.Create();
        hasher.Append(input);

        Assert.Equal(expected, hasher.Digest());
    }

    public static TheoryData<int> FixedLengths => new()
    {
        0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65,
        100, 127, 128, 129, 255, 256, 257, 511, 512, 513, 999, 1000,
    };

    [Theory]
    [MemberData(nameof(FixedLengths))]
    public void Digest_FixedDataset_MatchesSystemIoHashingOracle(int length)
    {
        byte[] data = MakeFixedPattern(length);

        XxHash64 hasher = XxHash64.Create();
        hasher.Append(data);
        ulong actual = hasher.Digest();

        ulong expected = SystemIoHashing.XxHash64.HashToUInt64(data);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(FixedLengths))]
    public void Append_OneByteAtATime_MatchesSingleCallDigest(int length)
    {
        byte[] data = MakeFixedPattern(length);

        XxHash64 whole = XxHash64.Create();
        whole.Append(data);

        XxHash64 chunked = XxHash64.Create();
        foreach (byte b in data)
        {
            chunked.Append([b]);
        }

        Assert.Equal(whole.Digest(), chunked.Digest());
    }

    [Theory]
    [MemberData(nameof(FixedLengths))]
    public void Append_SevenByteChunks_MatchesSingleCallDigest(int length)
    {
        byte[] data = MakeFixedPattern(length);

        XxHash64 whole = XxHash64.Create();
        whole.Append(data);

        XxHash64 chunked = XxHash64.Create();
        for (int offset = 0; offset < data.Length; offset += 7)
        {
            int take = Math.Min(7, data.Length - offset);
            chunked.Append(data.AsSpan(offset, take));
        }

        Assert.Equal(whole.Digest(), chunked.Digest());
    }

    /// <summary>
    /// Explicitly exercises chunk boundaries that land exactly on, just before, and just after the
    /// internal 32-byte carry buffer's edge, across several different chunk sizes.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    public void Append_VariousChunkSizesAcross32ByteBoundary_MatchesSingleCallDigest(int chunkSize)
    {
        byte[] data = MakeFixedPattern(97); // 3 * 32 + 1, guarantees multiple boundary crossings

        XxHash64 whole = XxHash64.Create();
        whole.Append(data);

        XxHash64 chunked = XxHash64.Create();
        for (int offset = 0; offset < data.Length; offset += chunkSize)
        {
            int take = Math.Min(chunkSize, data.Length - offset);
            chunked.Append(data.AsSpan(offset, take));
        }

        Assert.Equal(whole.Digest(), chunked.Digest());
    }

    [Fact]
    public void Append_CalledWithEmptySpan_DoesNotAffectResult()
    {
        byte[] data = MakeFixedPattern(50);

        XxHash64 plain = XxHash64.Create();
        plain.Append(data);

        XxHash64 withEmptyAppends = XxHash64.Create();
        withEmptyAppends.Append([]);
        withEmptyAppends.Append(data.AsSpan(0, 20));
        withEmptyAppends.Append([]);
        withEmptyAppends.Append(data.AsSpan(20));
        withEmptyAppends.Append([]);

        Assert.Equal(plain.Digest(), withEmptyAppends.Digest());
    }

    private static byte[] MakeFixedPattern(int length)
    {
        var data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = unchecked((byte)((i * 31) + 7));
        }

        return data;
    }
}
