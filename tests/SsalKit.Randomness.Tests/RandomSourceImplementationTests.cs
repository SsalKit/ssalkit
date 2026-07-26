namespace SsalKit.Randomness.Tests;

/// <summary>
/// Basic behavioral tests for the three built-in <see cref="IRandomSource"/> implementations:
/// <see cref="CryptoRandomSource"/>, <see cref="SharedRandomSource"/>, and
/// <see cref="SystemRandomSource"/>.
/// </summary>
public class RandomSourceImplementationTests
{
    [Fact]
    public void CryptoRandomSource_Instance_IsSingleton()
    {
        Assert.Same(CryptoRandomSource.Instance, CryptoRandomSource.Instance);
    }

    [Fact]
    public void CryptoRandomSource_NextUInt64_DoesNotThrow()
    {
        ulong value = CryptoRandomSource.Instance.NextUInt64();

        // Not a meaningful assertion on the value itself (it's cryptographically random), but the
        // call must complete without throwing.
        _ = value;
    }

    [Fact]
    public void CryptoRandomSource_NextBytes_FillsBuffer()
    {
        Span<byte> buffer = stackalloc byte[64];
        CryptoRandomSource.Instance.NextBytes(buffer);

        // Overwhelmingly unlikely for 64 cryptographically random bytes to all be zero.
        bool anyNonZero = false;
        foreach (byte b in buffer)
        {
            if (b != 0)
            {
                anyNonZero = true;
                break;
            }
        }

        Assert.True(anyNonZero);
    }

    [Fact]
    public void CryptoRandomSource_NextBytes_EmptyBuffer_DoesNotThrow()
    {
        CryptoRandomSource.Instance.NextBytes(Span<byte>.Empty);
    }

    [Fact]
    public void SharedRandomSource_Instance_IsSingleton()
    {
        Assert.Same(SharedRandomSource.Instance, SharedRandomSource.Instance);
    }

    [Fact]
    public void SharedRandomSource_NextUInt64_DoesNotThrow()
    {
        ulong value = SharedRandomSource.Instance.NextUInt64();
        _ = value;
    }

    [Fact]
    public void SharedRandomSource_NextBytes_FillsBuffer()
    {
        Span<byte> buffer = stackalloc byte[64];
        SharedRandomSource.Instance.NextBytes(buffer);

        bool anyNonZero = false;
        foreach (byte b in buffer)
        {
            if (b != 0)
            {
                anyNonZero = true;
                break;
            }
        }

        Assert.True(anyNonZero);
    }

    [Fact]
    public void SharedRandomSource_NextBytes_EmptyBuffer_DoesNotThrow()
    {
        SharedRandomSource.Instance.NextBytes(Span<byte>.Empty);
    }

    [Fact]
    public void SystemRandomSource_NullRandom_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemRandomSource(null!));
    }

    [Fact]
    public void SystemRandomSource_SeededRandom_IsReproducible()
    {
        var a = new SystemRandomSource(new Random(42));
        var b = new SystemRandomSource(new Random(42));

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    [Fact]
    public void SystemRandomSource_SeededRandom_NextBytesIsReproducible()
    {
        var a = new SystemRandomSource(new Random(7));
        var b = new SystemRandomSource(new Random(7));

        Span<byte> bufferA = stackalloc byte[32];
        Span<byte> bufferB = stackalloc byte[32];
        a.NextBytes(bufferA);
        b.NextBytes(bufferB);

        Assert.True(bufferA.SequenceEqual(bufferB));
    }

    [Fact]
    public void SystemRandomSource_NextBytes_EmptyBuffer_DoesNotThrow()
    {
        var source = new SystemRandomSource(new Random(1));

        source.NextBytes(Span<byte>.Empty);
    }

    // ---- Concurrency smoke tests for the two sources documented as thread-safe ----
    //
    // Mirrors the Parallel.For style WeightedSamplerTests uses for the same purpose: the point is
    // not to prove thread safety (no test can) but to pin the documented claim against an
    // implementation that starts using shared mutable state, which would surface as a throw or a
    // buffer that came back untouched.

    private const int ConcurrentWorkerCount = 8;
    private const int DrawsPerWorker = 500;

    [Fact]
    public void SharedRandomSource_ConcurrentDraws_DoNotThrowAndAlwaysProduceOutput()
    {
        AssertConcurrentDrawsSucceed(SharedRandomSource.Instance);
    }

    [Fact]
    public void CryptoRandomSource_ConcurrentDraws_DoNotThrowAndAlwaysProduceOutput()
    {
        AssertConcurrentDrawsSucceed(CryptoRandomSource.Instance);
    }

    /// <summary>
    /// Hammers one shared <see cref="IRandomSource"/> instance from several threads through both
    /// interface members, and asserts every worker got a full set of draws back.
    /// </summary>
    /// <remarks>
    /// The "all zero" check on the byte buffers is the strongest assertion available without
    /// assuming a distribution: for a 32-byte buffer it is astronomically unlikely from either
    /// source, so an all-zero buffer means the fill never happened.
    /// </remarks>
    private static void AssertConcurrentDrawsSucceed(IRandomSource source)
    {
        var scalarDraws = new ulong[ConcurrentWorkerCount][];
        var byteDraws = new byte[ConcurrentWorkerCount][];

        System.Threading.Tasks.Parallel.For(0, ConcurrentWorkerCount, worker =>
        {
            var scalars = new ulong[DrawsPerWorker];
            for (int i = 0; i < DrawsPerWorker; i++)
            {
                scalars[i] = source.NextUInt64();
            }

            var buffer = new byte[32];
            source.NextBytes(buffer);

            scalarDraws[worker] = scalars;
            byteDraws[worker] = buffer;
        });

        for (int worker = 0; worker < ConcurrentWorkerCount; worker++)
        {
            Assert.Equal(DrawsPerWorker, scalarDraws[worker].Length);
            Assert.Contains(byteDraws[worker], b => b != 0);
        }
    }
}
