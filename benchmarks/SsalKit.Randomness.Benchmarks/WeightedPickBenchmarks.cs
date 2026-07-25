using BenchmarkDotNet.Attributes;
using SsalKit.Randomness;

namespace SsalKit.Randomness.Benchmarks;

/// <summary>
/// Compares the three ways to draw a single weighted pick from an <c>N</c>-item table: the
/// delegate-based <see cref="WeightedRandomExtensions.PickWeighted{T}(IRandomSource, IReadOnlyList{T}, Func{T, long})"/>
/// overload (rebuilds the cumulative-sum array and allocates every call), the allocation-free
/// span-based overload (rebuilds the cumulative-sum array on the stack/heap but returns without
/// allocating), and <see cref="WeightedSampler{T}.Pick(IRandomSource)"/> against a pre-built alias
/// table (O(1), no per-call table construction) as the baseline.
/// </summary>
[MemoryDiagnoser]
public class WeightedPickBenchmarks
{
    [Params(10, 100, 1000)]
    public int N { get; set; }

    private DeterministicRandom _det = null!;
    private int[] _items = null!;
    private long[] _weights = null!;
    private Func<int, long> _weightSelector = null!;
    private WeightedSampler<int> _sampler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _det = new DeterministicRandom(42);

        _items = new int[N];
        _weights = new long[N];
        for (int i = 0; i < N; i++)
        {
            _items[i] = i;
            _weights[i] = i + 1; // deliberately non-uniform weights
        }

        _weightSelector = static item => item + 1L;
        _sampler = WeightedSampler<int>.Create(_items, _weights.AsSpan());
    }

    [Benchmark]
    public int PickWeighted_Delegate() => _det.PickWeighted(_items, _weightSelector);

    [Benchmark]
    public int PickWeighted_Span() => _det.PickWeighted(_items.AsSpan(), _weights.AsSpan());

    [Benchmark(Baseline = true)]
    public int Sampler_Pick() => _sampler.Pick(_det);
}
