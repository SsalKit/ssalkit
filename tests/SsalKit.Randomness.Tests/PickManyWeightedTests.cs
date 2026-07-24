namespace SsalKit.Randomness.Tests;

/// <summary>
/// Functional tests for <see cref="WeightedRandomExtensions.PickManyWeighted{T}(IRandomSource, IReadOnlyList{T}, Func{T, long}, int)"/>
/// (with replacement): result length, fixed-seed reproducibility, and — most importantly — that
/// the batched implementation (which builds the cumulative-sum array once and reuses it) draws
/// from the underlying source in exactly the same order as calling the single-item
/// <c>PickWeighted</c> in a loop, so the two produce bit-identical sequences for identical
/// starting state.
/// </summary>
public class PickManyWeightedTests
{
    private static readonly string[] Items = ["a", "b", "c", "d"];

    [Fact]
    public void PickManyWeighted_ReturnsArrayOfRequestedLength()
    {
        var random = new DeterministicRandom(1UL);
        string[] result = random.PickManyWeighted(Items, static x => (long)x.Length + 1, 25);

        Assert.Equal(25, result.Length);
    }

    [Fact]
    public void PickManyWeighted_CountOne_ReturnsSingleElementArray()
    {
        var random = new DeterministicRandom(1UL);
        string[] result = random.PickManyWeighted(Items, static x => (long)x.Length + 1, 1);

        Assert.Single(result);
    }

    [Fact]
    public void PickManyWeighted_CanReturnDuplicates()
    {
        // With replacement and a large enough draw count against a small item set, duplicates are
        // overwhelmingly likely; a single dominant-weight item makes this deterministic in
        // practice without relying on luck.
        var random = new DeterministicRandom(1UL);
        string[] items = ["dominant", "rare"];
        long[] weights = [1_000_000, 1];

        string[] result = random.PickManyWeighted(items, x => weights[Array.IndexOf(items, x)], 50);

        Assert.Contains("dominant", result);
        Assert.True(result.Count(x => x == "dominant") > 1);
    }

    [Fact]
    public void PickManyWeighted_SameSeed_IsReproducible()
    {
        var a = new DeterministicRandom(4242UL);
        var b = new DeterministicRandom(4242UL);

        string[] resultA = a.PickManyWeighted(Items, static x => (long)x.Length + 1, 100);
        string[] resultB = b.PickManyWeighted(Items, static x => (long)x.Length + 1, 100);

        Assert.Equal(resultA, resultB);
    }

    [Fact]
    public void PickManyWeighted_MatchesLoopOfSingleItemPickWeighted_BitIdentical()
    {
        const int count = 500;
        long[] weights = [1, 2, 3, 4];
        string[] items = ["a", "b", "c", "d"];

        var batchedSource = new DeterministicRandom(777UL);
        string[] batched = batchedSource.PickManyWeighted(items, x => weights[Array.IndexOf(items, x)], count);

        var loopedSource = new DeterministicRandom(777UL);
        var looped = new string[count];
        for (int i = 0; i < count; i++)
        {
            looped[i] = loopedSource.PickWeighted(items, x => weights[Array.IndexOf(items, x)]);
        }

        Assert.Equal(looped, batched);
    }

    [Fact]
    public void PickManyWeighted_MatchesLoopOfSingleItemPickWeighted_SpanOverload_BitIdentical()
    {
        const int count = 500;
        long[] weights = [1, 2, 3, 4];
        string[] items = ["a", "b", "c", "d"];

        var batchedSource = new DeterministicRandom(999UL);
        string[] batched = batchedSource.PickManyWeighted(items, x => weights[Array.IndexOf(items, x)], count);

        var loopedSource = new DeterministicRandom(999UL);
        var looped = new string[count];
        for (int i = 0; i < count; i++)
        {
            looped[i] = loopedSource.PickWeighted(items.AsSpan(), weights.AsSpan());
        }

        Assert.Equal(looped, batched);
    }
}
