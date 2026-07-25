using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SsalKit.Randomness;

namespace SsalKit.Randomness.Benchmarks;

/// <summary>
/// Compares uniform-generation throughput and allocations across four random sources:
/// <see cref="DeterministicRandom"/> (the baseline in every category), the BCL's seeded
/// <see cref="System.Random"/>, <see cref="Random.Shared"/>, and <see cref="CryptoRandomSource"/>.
/// Benchmarks are grouped by operation category so BenchmarkDotNet reports a per-category ratio
/// against the <see cref="DeterministicRandom"/> baseline.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class UniformGenerationBenchmarks
{
    private const string CategoryNextUInt64 = "01_NextUInt64";
    private const string CategoryNext1000 = "02_Next1000";
    private const string CategoryNextRange = "03_NextRange";
    private const string CategoryNextInt64Bounded = "04_NextInt64Bounded";
    private const string CategoryNextDouble = "05_NextDouble";
    private const string CategoryNextBytes = "06_NextBytes";

    private DeterministicRandom _det = null!;
    private Random _systemRandom = null!;
    private CryptoRandomSource _crypto = null!;
    private readonly byte[] _buffer = new byte[64];

    [GlobalSetup]
    public void Setup()
    {
        _det = new DeterministicRandom(42);
        _systemRandom = new Random(42);
        _crypto = CryptoRandomSource.Instance;
    }

    // -----------------------------------------------------------------
    // NextUInt64 (System.Random has no NextUInt64, so NextInt64() stands in for it)
    // -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(CategoryNextUInt64)]
    public ulong DeterministicRandom_NextUInt64() => _det.NextUInt64();

    [Benchmark]
    [BenchmarkCategory(CategoryNextUInt64)]
    public long SystemRandom_NextInt64() => _systemRandom.NextInt64();

    [Benchmark]
    [BenchmarkCategory(CategoryNextUInt64)]
    public long RandomShared_NextInt64() => Random.Shared.NextInt64();

    [Benchmark]
    [BenchmarkCategory(CategoryNextUInt64)]
    public ulong Crypto_NextUInt64() => _crypto.NextUInt64();

    // -----------------------------------------------------------------
    // Next(1000) -- bounded int
    // -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(CategoryNext1000)]
    public int DeterministicRandom_Next1000() => _det.Next(1000);

    [Benchmark]
    [BenchmarkCategory(CategoryNext1000)]
    public int SystemRandom_Next1000() => _systemRandom.Next(1000);

    [Benchmark]
    [BenchmarkCategory(CategoryNext1000)]
    public int RandomShared_Next1000() => Random.Shared.Next(1000);

    [Benchmark]
    [BenchmarkCategory(CategoryNext1000)]
    public int Crypto_Next1000() => _crypto.Next(1000);

    // -----------------------------------------------------------------
    // Next(-500, 500)
    // -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(CategoryNextRange)]
    public int DeterministicRandom_NextRange() => _det.Next(-500, 500);

    [Benchmark]
    [BenchmarkCategory(CategoryNextRange)]
    public int SystemRandom_NextRange() => _systemRandom.Next(-500, 500);

    [Benchmark]
    [BenchmarkCategory(CategoryNextRange)]
    public int RandomShared_NextRange() => Random.Shared.Next(-500, 500);

    [Benchmark]
    [BenchmarkCategory(CategoryNextRange)]
    public int Crypto_NextRange() => _crypto.Next(-500, 500);

    // -----------------------------------------------------------------
    // NextInt64(1L << 40)
    // -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(CategoryNextInt64Bounded)]
    public long DeterministicRandom_NextInt64Bounded() => _det.NextInt64(1L << 40);

    [Benchmark]
    [BenchmarkCategory(CategoryNextInt64Bounded)]
    public long SystemRandom_NextInt64Bounded() => _systemRandom.NextInt64(1L << 40);

    [Benchmark]
    [BenchmarkCategory(CategoryNextInt64Bounded)]
    public long RandomShared_NextInt64Bounded() => Random.Shared.NextInt64(1L << 40);

    [Benchmark]
    [BenchmarkCategory(CategoryNextInt64Bounded)]
    public long Crypto_NextInt64Bounded() => _crypto.NextInt64(1L << 40);

    // -----------------------------------------------------------------
    // NextDouble
    // -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(CategoryNextDouble)]
    public double DeterministicRandom_NextDouble() => _det.NextDouble();

    [Benchmark]
    [BenchmarkCategory(CategoryNextDouble)]
    public double SystemRandom_NextDouble() => _systemRandom.NextDouble();

    [Benchmark]
    [BenchmarkCategory(CategoryNextDouble)]
    public double RandomShared_NextDouble() => Random.Shared.NextDouble();

    [Benchmark]
    [BenchmarkCategory(CategoryNextDouble)]
    public double Crypto_NextDouble() => _crypto.NextDouble();

    // -----------------------------------------------------------------
    // NextBytes(64-byte buffer)
    // -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(CategoryNextBytes)]
    public byte DeterministicRandom_NextBytes()
    {
        _det.NextBytes(_buffer);
        return _buffer[0];
    }

    [Benchmark]
    [BenchmarkCategory(CategoryNextBytes)]
    public byte SystemRandom_NextBytes()
    {
        _systemRandom.NextBytes(_buffer);
        return _buffer[0];
    }

    [Benchmark]
    [BenchmarkCategory(CategoryNextBytes)]
    public byte RandomShared_NextBytes()
    {
        Random.Shared.NextBytes(_buffer);
        return _buffer[0];
    }

    [Benchmark]
    [BenchmarkCategory(CategoryNextBytes)]
    public byte Crypto_NextBytes()
    {
        _crypto.NextBytes(_buffer);
        return _buffer[0];
    }
}
