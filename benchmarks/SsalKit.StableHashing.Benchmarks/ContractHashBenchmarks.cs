using BenchmarkDotNet.Attributes;

namespace SsalKit.StableHashing.Benchmarks;

/// <summary>
/// Measures <c>ComputeStableHash()</c> on a small (4 scalar members) and a medium (12 members,
/// including two strings and a nested contract) contract. <see cref="MemoryDiagnoser"/> is the
/// point of this class: it exists to make the <c>Allocated</c> column read 0 B for both, proving
/// the streaming-writer design (design doc section 4.6) never allocates per call.
/// </summary>
[MemoryDiagnoser]
public class ContractHashBenchmarks
{
    private SmallContract _small = null!;
    private MediumContract _medium = null!;

    [GlobalSetup]
    public void Setup()
    {
        _small = new SmallContract { Id = 42, Value = 123_456_789L, Flag = true, Ratio = 3.14159 };
        _medium = BenchmarkFixtures.CreateMedium();
    }

    [Benchmark(Baseline = true)]
    public ulong Small() => _small.ComputeStableHash().Value;

    [Benchmark]
    public ulong Medium() => _medium.ComputeStableHash().Value;
}
