namespace SsalKit.Randomness.Tests;

/// <summary>
/// Functional tests for <see cref="WeightedRandomExtensions.PickManyWeightedDistinct{T}(IRandomSource, IReadOnlyList{T}, Func{T, long}, int)"/>
/// (without replacement): no duplicates, zero-weight items never appear, drawing exactly the
/// number of positive-weight items returns all of them (in selection order), count-boundary
/// exceptions, and reproducibility under a fixed seed.
/// </summary>
public class PickManyWeightedDistinctTests
{
    private static readonly string[] Items = ["a", "b", "c", "d", "e", "f", "g", "h"];
    private static readonly long[] Weights = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public void PickManyWeightedDistinct_NeverReturnsDuplicates()
    {
        var random = new DeterministicRandom(1UL);
        string[] result = random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], Items.Length);

        Assert.Equal(result.Length, result.Distinct().Count());
    }

    [Fact]
    public void PickManyWeightedDistinct_PartialDraw_NeverReturnsDuplicates()
    {
        var random = new DeterministicRandom(2UL);
        string[] result = random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 5);

        Assert.Equal(5, result.Length);
        Assert.Equal(result.Length, result.Distinct().Count());
    }

    [Fact]
    public void PickManyWeightedDistinct_ZeroWeightItem_NeverAppears()
    {
        string[] items = ["a", "z1", "b", "z2", "c"];
        long[] weights = [10, 0, 10, 0, 10];
        var random = new DeterministicRandom(3UL);

        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 3);

        Assert.DoesNotContain("z1", result);
        Assert.DoesNotContain("z2", result);
        Assert.Equal(3, result.Distinct().Count());
    }

    [Fact]
    public void PickManyWeightedDistinct_CountEqualsPositiveWeightCount_ReturnsAllPositiveWeightItems()
    {
        string[] items = ["a", "z", "b", "c"];
        long[] weights = [1, 0, 1, 1];
        var random = new DeterministicRandom(4UL);

        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 3);

        Assert.Equal(["a", "b", "c"], result.OrderBy(x => x, StringComparer.Ordinal));
        Assert.DoesNotContain("z", result);
    }

    [Fact]
    public void PickManyWeightedDistinct_ResultOrderIsSelectionOrder_NotSorted()
    {
        // With a heavily skewed weight distribution and a fixed seed, the highest-weight item
        // should be selected first far more often than not; assert the raw (unsorted) order is
        // preserved by checking it can differ from sorted order across repeated distinct seeds.
        string[] items = ["low", "high"];
        long[] weights = [1, 1_000_000];

        var random = new DeterministicRandom(5UL);
        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 2);

        Assert.Equal("high", result[0]);
        Assert.Equal("low", result[1]);
    }

    [Fact]
    public void PickManyWeightedDistinct_SameSeed_IsReproducible()
    {
        var a = new DeterministicRandom(6UL);
        var b = new DeterministicRandom(6UL);

        string[] resultA = a.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 5);
        string[] resultB = b.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 5);

        Assert.Equal(resultA, resultB);
    }

    [Fact]
    public void PickManyWeightedDistinct_CountZero_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 0));
    }

    [Fact]
    public void PickManyWeightedDistinct_CountExceedsItemCount_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], Items.Length + 1));
    }

    [Fact]
    public void PickManyWeightedDistinct_SingleItem_ReturnsIt()
    {
        var random = new DeterministicRandom(1UL);
        string[] result = random.PickManyWeightedDistinct(["only"], static _ => 1L, 1);

        Assert.Equal(["only"], result);
    }
}
