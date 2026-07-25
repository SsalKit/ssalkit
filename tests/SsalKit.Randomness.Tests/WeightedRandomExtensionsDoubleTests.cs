namespace SsalKit.Randomness.Tests;

/// <summary>
/// Covers the happy path of the <see cref="double"/>-weighted overloads of
/// <see cref="WeightedRandomExtensions"/> — <c>PickWeighted&lt;T&gt;(IRandomSource, IReadOnlyList{T}, Func{T, double})</c>
/// and <c>PickWeighted&lt;T&gt;(IRandomSource, ReadOnlySpan{T}, ReadOnlySpan{double})</c> — which the
/// exception-contract tests in <c>WeightedRandomExtensionsExceptionTests</c> and the null-source
/// tests never exercise to a successful return. Mirrors the deterministic-boundary style of
/// <c>WeightedRandomExtensionsBoundaryTests</c> (long weights) and the chi-square style of
/// <c>WeightedDistributionTests</c>, plus the heap-fallback path (<c>MaxStackAllocElements</c>
/// exceeded) for both the long- and double-weighted span overloads.
/// </summary>
public class WeightedRandomExtensionsDoubleTests
{
    /// <summary>
    /// Stubbed <see cref="IRandomSource"/> that replays a fixed sequence of raw
    /// <see cref="NextUInt64"/> values.
    /// </summary>
    private sealed class QueueRandomSource(ulong[] values) : IRandomSource
    {
        private int _index;

        public ulong NextUInt64()
        {
            if (_index >= values.Length)
            {
                throw new InvalidOperationException("Stub sequence exhausted: an unexpected extra draw occurred.");
            }

            return values[_index++];
        }

        public void NextBytes(Span<byte> buffer) => throw new NotSupportedException("Not exercised by these tests.");
    }

    /// <summary>
    /// Computes a raw 64-bit value that makes <c>NextDouble()</c> (<c>(raw &gt;&gt; 11) * 2^-53</c>)
    /// return exactly <paramref name="fraction"/>, provided <paramref name="fraction"/> is an exact
    /// multiple of a small negative power of two (as every fraction used below is, being
    /// <c>k / 128</c>) so that <c>fraction * 2^53 == k * 2^46</c> is itself an exact integer with no
    /// rounding involved.
    /// </summary>
    private static ulong RawValueForFraction(double fraction) => (ulong)(fraction * (1UL << 53)) << 11;

    // ---------------------------------------------------------------------
    // Deterministic boundary tests — symmetric with WeightedRandomExtensionsBoundaryTests (long)
    // ---------------------------------------------------------------------
    //
    // Weights [64, 32, 16, 16] give a power-of-two total (128) and power-of-two cumulative sums
    // (64, 96, 112, 128), so every bucket boundary used below is an exact double value with no
    // floating-point rounding ambiguity — unlike an arbitrary total, which would make "just below
    // the boundary" imprecise to construct deterministically.

    private static readonly string[] BoundaryItems = ["a", "b", "c", "d"];
    private static readonly double[] BoundaryWeights = [64.0, 32.0, 16.0, 16.0];

    [Fact]
    public void PickWeighted_SpanDouble_PositionZero_SelectsFirstItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(0.0 / 128)]);
        Assert.Equal("a", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionJustBelowFirstBoundary_SelectsFirstItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(63.0 / 128)]);
        Assert.Equal("a", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionAtFirstBoundary_SelectsSecondItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(64.0 / 128)]);
        Assert.Equal("b", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionJustBelowSecondBoundary_SelectsSecondItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(95.0 / 128)]);
        Assert.Equal("b", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionAtSecondBoundary_SelectsThirdItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(96.0 / 128)]);
        Assert.Equal("c", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionJustBelowThirdBoundary_SelectsThirdItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(111.0 / 128)]);
        Assert.Equal("c", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionAtThirdBoundary_SelectsFourthItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(112.0 / 128)]);
        Assert.Equal("d", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_PositionJustBelowFinalBoundary_SelectsFourthItem()
    {
        var source = new QueueRandomSource([RawValueForFraction(127.0 / 128)]);
        Assert.Equal("d", source.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_IReadOnlyListDouble_Delegate_AgreesWithSpanOverloadAtEachBoundary()
    {
        // The delegate-based List overload shares the same BuildDoubleCumulative +
        // PickIndexFromDoubleCumulative + BinarySearchCumulativeDouble helpers as the span
        // overload, so the same raw draws must select the same buckets.
        (string Name, double Weight)[] items =
        [
            ("a", 64.0),
            ("b", 32.0),
            ("c", 16.0),
            ("d", 16.0),
        ];

        foreach (double fraction in new[] { 0.0 / 128, 63.0 / 128, 64.0 / 128, 95.0 / 128, 96.0 / 128, 111.0 / 128, 112.0 / 128, 127.0 / 128 })
        {
            var listSource = new QueueRandomSource([RawValueForFraction(fraction)]);
            var spanSource = new QueueRandomSource([RawValueForFraction(fraction)]);

            string viaList = listSource.PickWeighted(items, static x => x.Weight).Name;
            string viaSpan = spanSource.PickWeighted(BoundaryItems.AsSpan(), BoundaryWeights.AsSpan());

            Assert.Equal(viaSpan, viaList);
        }
    }

    [Fact]
    public void PickWeighted_SpanDouble_ZeroWeightItem_IsNeverSelectedOverManyDraws()
    {
        string[] items = ["a", "b", "z", "c"];
        double[] weights = [1.0, 2.0, 0.0, 3.0];

        var random = new DeterministicRandom(0xD00DUL);
        for (int i = 0; i < 5_000; i++)
        {
            Assert.NotEqual("z", random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        }
    }

    [Fact]
    public void PickWeighted_IReadOnlyListDouble_ZeroWeightItem_IsNeverSelectedOverManyDraws()
    {
        string[] items = ["a", "b", "z", "c"];
        double[] weights = [1.0, 2.0, 0.0, 3.0];

        var random = new DeterministicRandom(0xD00DUL);
        for (int i = 0; i < 5_000; i++)
        {
            Assert.NotEqual("z", random.PickWeighted(items, x => weights[Array.IndexOf(items, x)]));
        }
    }

    // ---------------------------------------------------------------------
    // Chi-square distribution smoke tests — symmetric with WeightedDistributionTests (long)
    // ---------------------------------------------------------------------

    private static readonly string[] SmokeItems = ["a", "b", "c"];
    private static readonly double[] SmokeWeights = [1.5, 3.0, 4.5];
    private const int SmokeSampleCount = 120_000;

    // Degrees of freedom = 2 (3 buckets - 1). Critical value for a very generous significance
    // level (p = 0.0001, chi2 = 18.42) is used to avoid flakiness while still catching gross bias.
    private const double SmokeCriticalValue = 18.42;

    [Fact]
    public void PickWeighted_SpanDouble_MatchesExpectedRatio_ChiSquareSmokeTest()
    {
        var random = new DeterministicRandom(0xC0FFEEUL);
        var observed = new int[SmokeItems.Length];
        for (int i = 0; i < SmokeSampleCount; i++)
        {
            observed[Array.IndexOf(SmokeItems, random.PickWeighted(SmokeItems.AsSpan(), SmokeWeights.AsSpan()))]++;
        }

        AssertFitsExpectedRatio(observed);
    }

    [Fact]
    public void PickWeighted_IReadOnlyListDouble_Delegate_MatchesExpectedRatio_ChiSquareSmokeTest()
    {
        var random = new DeterministicRandom(0xBADA55UL);
        var observed = new int[SmokeItems.Length];
        for (int i = 0; i < SmokeSampleCount; i++)
        {
            observed[Array.IndexOf(SmokeItems, random.PickWeighted(SmokeItems, static x => SmokeWeights[Array.IndexOf(SmokeItems, x)]))]++;
        }

        AssertFitsExpectedRatio(observed);
    }

    private static void AssertFitsExpectedRatio(int[] observed)
    {
        double total = 0;
        foreach (double w in SmokeWeights)
        {
            total += w;
        }

        double chiSquare = 0.0;
        for (int i = 0; i < observed.Length; i++)
        {
            double expected = SmokeSampleCount * (SmokeWeights[i] / total);
            double diff = observed[i] - expected;
            chiSquare += (diff * diff) / expected;
        }

        Assert.True(chiSquare < SmokeCriticalValue, $"chi-square statistic {chiSquare} exceeded critical value {SmokeCriticalValue}; counts=[{string.Join(", ", observed)}]");
    }

    // ---------------------------------------------------------------------
    // Heap fallback (element count exceeds MaxStackAllocElements = 256) for both span overloads
    // ---------------------------------------------------------------------

    private const int HeapFallbackElementCount = 300;
    private const int DominantWeightIndex = 150;

    [Fact]
    public void PickWeighted_SpanLong_HeapFallback_SingleNonZeroWeight_AlwaysSelectsThatItem()
    {
        int[] items = Enumerable.Range(0, HeapFallbackElementCount).ToArray();
        long[] weights = new long[HeapFallbackElementCount];
        weights[DominantWeightIndex] = 1;

        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(DominantWeightIndex, random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        }
    }

    [Fact]
    public void PickWeighted_SpanLong_HeapFallback_UniformWeights_IsReproducibleForSameSeed()
    {
        int[] items = Enumerable.Range(0, HeapFallbackElementCount).ToArray();
        long[] weights = Enumerable.Repeat(1L, HeapFallbackElementCount).ToArray();

        var randomA = new DeterministicRandom(2024UL);
        var randomB = new DeterministicRandom(2024UL);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(randomA.PickWeighted(items.AsSpan(), weights.AsSpan()), randomB.PickWeighted(items.AsSpan(), weights.AsSpan()));
        }
    }

    [Fact]
    public void PickWeighted_SpanDouble_HeapFallback_SingleNonZeroWeight_AlwaysSelectsThatItem()
    {
        int[] items = Enumerable.Range(0, HeapFallbackElementCount).ToArray();
        double[] weights = new double[HeapFallbackElementCount];
        weights[DominantWeightIndex] = 1.0;

        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(DominantWeightIndex, random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        }
    }

    [Fact]
    public void PickWeighted_SpanDouble_HeapFallback_UniformWeights_IsReproducibleForSameSeed()
    {
        int[] items = Enumerable.Range(0, HeapFallbackElementCount).ToArray();
        double[] weights = Enumerable.Repeat(1.0, HeapFallbackElementCount).ToArray();

        var randomA = new DeterministicRandom(2024UL);
        var randomB = new DeterministicRandom(2024UL);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(randomA.PickWeighted(items.AsSpan(), weights.AsSpan()), randomB.PickWeighted(items.AsSpan(), weights.AsSpan()));
        }
    }
}
