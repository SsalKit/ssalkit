using System.Text;
using BenchmarkDotNet.Attributes;

namespace SsalKit.StableHashing.Benchmarks;

/// <summary>
/// Measures <c>ComputeStableHash()</c> on a contract whose only member is a <see langword="string"/>,
/// across three encoding paths: a short ASCII string, a Korean (multi-byte UTF-8) string of similar
/// character length, and a long (300+ character) string whose UTF-8 byte count exceeds
/// <see cref="StableHashWriter"/>'s 256-byte stackalloc threshold, forcing the
/// <see cref="System.Buffers.ArrayPool{T}"/> fallback path. All three stay allocation-free on the
/// managed heap (design doc section 4.6) -- the pooled buffer used by the fallback path is rented,
/// not allocated.
/// </summary>
[MemoryDiagnoser]
public class StringEncodingBenchmarks
{
    private StringContract _ascii = null!;
    private StringContract _korean = null!;
    private StringContract _long = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ascii = new StringContract { Text = "hello-stable-hashing" };
        _korean = new StringContract { Text = "안녕하세요 스테이블해싱 벤치마크입니다" };
        _long = new StringContract { Text = BuildLongText() };
    }

    [Benchmark(Baseline = true)]
    public ulong Ascii() => _ascii.ComputeStableHash().Value;

    [Benchmark]
    public ulong Korean() => _korean.ComputeStableHash().Value;

    [Benchmark]
    public ulong Long() => _long.ComputeStableHash().Value;

    // 8 repetitions of a 47-character sentence -> 376 ASCII characters, comfortably past the
    // writer's 256-byte stackalloc threshold, so this exercises the ArrayPool fallback path.
    private static string BuildLongText()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            builder.Append("The quick brown fox jumps over the lazy dog. ");
        }

        return builder.ToString();
    }
}
