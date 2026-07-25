using BenchmarkDotNet.Attributes;
using SsalKit.Randomness;

namespace SsalKit.Randomness.Benchmarks;

/// <summary>
/// Measures the overhead (if any) of going through the <see cref="IRandomSource"/> extension-method
/// path versus calling <see cref="DeterministicRandom"/>'s own instance method directly, using the
/// exact same underlying instance for both so only the dispatch route differs.
/// </summary>
[MemoryDiagnoser]
public class DispatchBenchmarks
{
    private DeterministicRandom _det = null!;
    private IRandomSource _src = null!;

    [GlobalSetup]
    public void Setup()
    {
        _det = new DeterministicRandom(42);

        // Same instance as _det, referenced through the interface so calls below actually route
        // through RandomSourceExtensions rather than DeterministicRandom's own instance methods.
        _src = _det;
    }

    [Benchmark(Baseline = true)]
    public int DirectInstanceCall() => _det.Next(1000);

    [Benchmark]
    public int InterfaceExtensionCall() => _src.Next(1000);
}
