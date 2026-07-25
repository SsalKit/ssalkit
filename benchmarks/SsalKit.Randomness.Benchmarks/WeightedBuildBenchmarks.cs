using BenchmarkDotNet.Attributes;
using SsalKit.Randomness;

namespace SsalKit.Randomness.Benchmarks;

/// <summary>
/// Measures the one-time cost of building a <see cref="WeightedSampler{T}"/> (the Walker/Vose
/// alias table construction) over item counts of increasing size, isolated from any draw-time
/// cost.
/// </summary>
[MemoryDiagnoser]
public class WeightedBuildBenchmarks
{
    [Params(10, 100, 1000)]
    public int N { get; set; }

    private int[] _items = null!;
    private Func<int, long> _weightSelector = null!;

    [GlobalSetup]
    public void Setup()
    {
        _items = new int[N];
        for (int i = 0; i < N; i++)
        {
            _items[i] = i;
        }

        _weightSelector = static item => item + 1L; // deliberately non-uniform weights
    }

    [Benchmark]
    public WeightedSampler<int> Create() => WeightedSampler<int>.Create(_items, _weightSelector);
}
