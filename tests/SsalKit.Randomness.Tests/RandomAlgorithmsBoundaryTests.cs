namespace SsalKit.Randomness.Tests;

/// <summary>
/// Precise, deterministic unit tests for <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/>'s
/// Lemire multiply-shift-reject logic, injecting exact raw 64-bit values via a stubbed
/// <see cref="IUInt64Generator"/> struct so the rejection threshold can be exercised on purpose
/// rather than relying on statistics.
///
/// All raw input values below were derived analytically for <c>bound = 3</c>, for which
/// <c>threshold = (2^64 mod 3) = 1</c> (verified independently: <c>0UL - 3UL) % 3UL == 1</c>):
///
/// - <c>0x0000000000000000</c> * 3 has low 64 bits == 0, which is &lt; bound (3) and &lt; threshold
///   (1) -&gt; must be rejected and redrawn.
/// - <c>0xAAAAAAAAAAAAAAAB</c> is the modular inverse of 3 mod 2^64, so <c>x * 3</c> has low 64
///   bits == 1 (== threshold) -&gt; must be accepted immediately, with high 64 bits == 2.
/// - <c>0x5555555555555556</c> (== 2 * that inverse) has low 64 bits == 2 (&lt; bound, &gt;=
///   threshold) -&gt; must be accepted immediately, with high 64 bits == 1.
/// - <c>0xFFFFFFFFFFFFFFFF</c> has low 64 bits == 18446744073709551613, which is &gt;= bound (3)
///   -&gt; the rejection branch is skipped entirely (fast path), with high 64 bits == 2.
/// </summary>
public class RandomAlgorithmsBoundaryTests
{
    /// <summary>
    /// Stubbed <see cref="IUInt64Generator"/> that replays a fixed sequence of raw values. A
    /// mutable struct passed by <c>ref</c> to <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/>
    /// so its index advances correctly across redraws within a single call.
    /// </summary>
    private struct QueueGenerator(ulong[] values) : IUInt64Generator
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
    }

    /// <summary>
    /// Adapter wrapping a real <see cref="DeterministicRandom"/> instance via its public
    /// <see cref="DeterministicRandom.NextUInt64"/> method, for tests that need to drive
    /// <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/> from a genuine xoshiro256**
    /// sequence rather than a stub.
    /// </summary>
    private readonly struct DeterministicRandomGenerator(DeterministicRandom random) : IUInt64Generator
    {
        public ulong NextUInt64() => random.NextUInt64();
    }

    [Fact]
    public void NextUInt64Bounded_RawValueBelowThreshold_IsRejectedAndRedrawn()
    {
        // draw1 (0x0) is rejected; draw2 (0xAAAA...AB) is the accepted boundary case (low ==
        // threshold == 1), which maps to high 64 bits == 2.
        var generator = new QueueGenerator([0x0000000000000000UL, 0xAAAAAAAAAAAAAAABUL]);

        ulong result = RandomAlgorithms.NextUInt64Bounded(ref generator, bound: 3);

        Assert.Equal(2UL, result);
    }

    [Fact]
    public void NextUInt64Bounded_RawValueAtThreshold_IsAcceptedWithoutRedraw()
    {
        // low == threshold == 1 must be accepted on the first draw (only one value is available;
        // a redraw attempt would throw).
        var generator = new QueueGenerator([0xAAAAAAAAAAAAAAABUL]);

        ulong result = RandomAlgorithms.NextUInt64Bounded(ref generator, bound: 3);

        Assert.Equal(2UL, result);
    }

    [Fact]
    public void NextUInt64Bounded_RawValueBelowBoundButAboveThreshold_IsAcceptedWithoutRedraw()
    {
        // low == 2 (< bound == 3, but >= threshold == 1) must be accepted on the first draw.
        var generator = new QueueGenerator([0x5555555555555556UL]);

        ulong result = RandomAlgorithms.NextUInt64Bounded(ref generator, bound: 3);

        Assert.Equal(1UL, result);
    }

    [Fact]
    public void NextUInt64Bounded_RawValueWithLowAtOrAboveBound_SkipsRejectionBranchEntirely()
    {
        // low >= bound entirely bypasses the rejection branch (the common case).
        var generator = new QueueGenerator([0xFFFFFFFFFFFFFFFFUL]);

        ulong result = RandomAlgorithms.NextUInt64Bounded(ref generator, bound: 3);

        Assert.Equal(2UL, result);
    }

    [Fact]
    public void NextUInt64Bounded_GenericValue_MapsToExpectedHighBits()
    {
        var generator = new QueueGenerator([0x1000000000000000UL]);

        ulong result = RandomAlgorithms.NextUInt64Bounded(ref generator, bound: 3);

        Assert.Equal(0UL, result);
    }

    [Fact]
    public void NextUInt64Bounded_ResultIsAlwaysWithinBound()
    {
        var random = new DeterministicRandom(2024UL);
        var generator = new DeterministicRandomGenerator(random);
        for (int i = 0; i < 10_000; i++)
        {
            ulong result = RandomAlgorithms.NextUInt64Bounded(ref generator, bound: 7);
            Assert.True(result < 7);
        }
    }
}
