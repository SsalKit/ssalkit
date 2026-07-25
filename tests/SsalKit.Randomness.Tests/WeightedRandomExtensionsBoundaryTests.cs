namespace SsalKit.Randomness.Tests;

/// <summary>
/// Precise, deterministic boundary tests for the <see cref="long"/>-weighted
/// <see cref="WeightedRandomExtensions.PickWeighted{T}(IRandomSource, ReadOnlySpan{T}, ReadOnlySpan{long})"/>
/// and <see cref="WeightedRandomExtensions.PickWeighted{T}(IRandomSource, IReadOnlyList{T}, Func{T, long})"/>
/// overloads, injecting exact raw 64-bit values via a stubbed <see cref="IRandomSource"/> so the
/// cumulative-sum bucket boundaries can be exercised on purpose rather than relying on
/// statistics — mirroring the approach in <c>RandomAlgorithmsBoundaryTests</c>.
/// </summary>
public class WeightedRandomExtensionsBoundaryTests
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
    /// Computes a raw 64-bit value that makes <c>RandomAlgorithms.NextUInt64Bounded(bound, ...)</c>
    /// return exactly <paramref name="position"/>, on its first draw with no rejection redraw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>NextUInt64Bounded</c> computes <c>product = raw * bound</c> (as a 128-bit value) and
    /// returns <c>high64(product)</c> once <c>low64(product)</c> either lands at or above
    /// <c>bound</c>, or lands in <c>[threshold, bound)</c>. For a target <paramref name="position"/>
    /// <c>p</c>, any <c>raw</c> with <c>product</c> in <c>[p * 2^64, (p + 1) * 2^64)</c> yields
    /// <c>high64(product) == p</c>. Let <c>rawMin = ceil(p * 2^64 / bound)</c> be the smallest such
    /// <c>raw</c>; by construction <c>low64(rawMin * bound)</c> already lies in <c>[0, bound)</c>
    /// (that is exactly what made <c>rawMin</c> the smallest value landing in this product range).
    /// Taking <c>raw = rawMin + 1</c> adds another whole <c>bound</c> to the product while staying
    /// in the same <c>[p * 2^64, (p + 1) * 2^64)</c> window (since <c>bound</c> is tiny relative to
    /// <c>2^64</c>), pushing <c>low64</c> to <c>[bound, 2 * bound)</c> — at or above <c>bound</c> —
    /// which is exactly the fast path that always accepts on the first draw, without needing to
    /// reason about the rejection threshold at all.
    /// </para>
    /// </remarks>
    private static ulong RawValueForPosition(ulong bound, ulong position)
    {
        UInt128 twoPow64 = (UInt128)1 << 64;
        UInt128 numerator = (UInt128)position * twoPow64;
        UInt128 rawMin = (numerator + bound - 1) / bound; // ceiling division
        UInt128 raw = rawMin + 1;
        return (ulong)raw;
    }

    [Fact]
    public void PickWeighted_Span_PositionJustBelowFirstBoundary_SelectsFirstItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 59)]);

        Assert.Equal("a", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_Span_PositionAtFirstBoundary_SelectsSecondItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 60)]);

        Assert.Equal("b", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_Span_PositionJustBelowSecondBoundary_SelectsSecondItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 89)]);

        Assert.Equal("b", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_Span_PositionAtSecondBoundary_SelectsThirdItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 90)]);

        Assert.Equal("c", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_Span_PositionJustBelowThirdBoundary_SelectsThirdItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 98)]);

        Assert.Equal("c", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_Span_PositionAtThirdBoundary_SelectsFourthItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 99)]);

        Assert.Equal("d", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_Span_PositionZero_SelectsFirstItem()
    {
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        var source = new QueueRandomSource([RawValueForPosition(100, 0)]);

        Assert.Equal("a", source.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_ExhaustiveSweep_EveryPositionMapsToExpectedBucket()
    {
        // Exhaustively drives every possible position in [0, 100) against the span overload and
        // confirms it lands in the exact cumulative-sum bucket implied by weights [60, 30, 9, 1]
        // (cumulative [60, 90, 99, 100]) — a full, deterministic proof of the bucket boundaries
        // rather than a handful of hand-picked samples.
        string[] items = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];
        long[] cumulative = [60, 90, 99, 100];

        for (ulong position = 0; position < 100; position++)
        {
            int expectedIndex = 0;
            while (cumulative[expectedIndex] <= (long)position)
            {
                expectedIndex++;
            }

            var source = new QueueRandomSource([RawValueForPosition(100, position)]);
            string actual = source.PickWeighted(items.AsSpan(), weights.AsSpan());

            Assert.Equal(items[expectedIndex], actual);
        }
    }

    [Fact]
    public void PickWeighted_IReadOnlyList_Delegate_AgreesWithSpanOverloadAtEachBoundary()
    {
        // The delegate-based List overload shares the same bounded-draw + binary-search helpers
        // as the span overload, so the same raw draws must select the same buckets.
        (string Name, long Weight)[] items =
        [
            ("a", 60),
            ("b", 30),
            ("c", 9),
            ("d", 1),
        ];
        string[] names = ["a", "b", "c", "d"];
        long[] weights = [60, 30, 9, 1];

        foreach (ulong position in new ulong[] { 0, 59, 60, 89, 90, 98, 99 })
        {
            var source = new QueueRandomSource([RawValueForPosition(100, position)]);
            var expectedSource = new QueueRandomSource([RawValueForPosition(100, position)]);

            string viaList = source.PickWeighted(items, static x => x.Weight).Name;
            string viaSpan = expectedSource.PickWeighted(names.AsSpan(), weights.AsSpan());

            Assert.Equal(viaSpan, viaList);
        }
    }

    [Fact]
    public void PickWeighted_ZeroWeightItem_IsNeverSelectedAcrossFullSweep()
    {
        // Items: a(60), b(30), c(9), z(0), d(1). z's bucket is empty ([99, 99)), so no position in
        // [0, 100) can ever land on it.
        string[] items = ["a", "b", "c", "z", "d"];
        long[] weights = [60, 30, 9, 0, 1];

        for (ulong position = 0; position < 100; position++)
        {
            var source = new QueueRandomSource([RawValueForPosition(100, position)]);
            string actual = source.PickWeighted(items.AsSpan(), weights.AsSpan());

            Assert.NotEqual("z", actual);
        }
    }

    [Fact]
    public void PickWeighted_ZeroWeightItem_PositionJustBelowFinalBoundary_SkipsToNextPositiveItem()
    {
        // position == 98 is the last position in c's bucket; the very next bucket belongs to d
        // (position 99), skipping z's empty bucket entirely.
        string[] items = ["a", "b", "c", "z", "d"];
        long[] weights = [60, 30, 9, 0, 1];

        var atEndOfC = new QueueRandomSource([RawValueForPosition(100, 98)]);
        Assert.Equal("c", atEndOfC.PickWeighted(items.AsSpan(), weights.AsSpan()));

        var atD = new QueueRandomSource([RawValueForPosition(100, 99)]);
        Assert.Equal("d", atD.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }
}
