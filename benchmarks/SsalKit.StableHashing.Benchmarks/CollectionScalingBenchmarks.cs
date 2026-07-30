using BenchmarkDotNet.Attributes;

namespace SsalKit.StableHashing.Benchmarks;

/// <summary>
/// Measures <c>ComputeStableHash()</c> on a contract whose only member is a <c>long[]</c>, across
/// element counts of 10, 100, and 1000, to demonstrate that hashing a collection member scales
/// linearly (O(n)) and stays allocation-free at every size (design doc section 4.6).
/// </summary>
[MemoryDiagnoser]
public class CollectionScalingBenchmarks
{
    [Params(10, 100, 1000)]
    public int N { get; set; }

    private CollectionContract _contract = null!;

    [GlobalSetup]
    public void Setup()
    {
        var values = new long[N];
        for (int i = 0; i < N; i++)
        {
            values[i] = i * 7L;
        }

        _contract = new CollectionContract { Values = values };
    }

    [Benchmark]
    public ulong ComputeHash() => _contract.ComputeStableHash().Value;
}
