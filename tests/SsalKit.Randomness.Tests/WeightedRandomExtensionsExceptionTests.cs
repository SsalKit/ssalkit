namespace SsalKit.Randomness.Tests;

/// <summary>
/// Exercises the exception contract shared by every <see cref="WeightedRandomExtensions"/> member
/// (design §3.6): empty items, negative weights, NaN/Infinity weights, zero total weight, long
/// overflow, invalid <c>count</c>, mismatched span lengths, and null arguments.
/// </summary>
public class WeightedRandomExtensionsExceptionTests
{
    private static readonly string[] Items = ["a", "b", "c"];
    private static readonly long[] LongWeights = [1, 2, 3];
    private static readonly double[] DoubleWeights = [1.0, 2.0, 3.0];

    // ---- PickWeighted<T>(IReadOnlyList<T>, Func<T, long>) ----

    [Fact]
    public void PickWeighted_ListLong_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.PickWeighted(Items, static x => (long)x.Length));
    }

    [Fact]
    public void PickWeighted_ListLong_NullItems_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickWeighted((IReadOnlyList<string>)null!, static x => (long)x.Length));
    }

    [Fact]
    public void PickWeighted_ListLong_NullWeight_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickWeighted(Items, (Func<string, long>)null!));
    }

    [Fact]
    public void PickWeighted_ListLong_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickWeighted((IReadOnlyList<string>)[], static x => (long)x.Length));
    }

    [Fact]
    public void PickWeighted_ListLong_NegativeWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items, x => weights[Array.IndexOf(items, x)]));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_ListLong_ZeroTotalWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickWeighted(Items, static _ => 0L));
    }

    // ---- PickWeighted<T>(IReadOnlyList<T>, Func<T, double>) ----

    [Fact]
    public void PickWeighted_ListDouble_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.PickWeighted(Items, static x => (double)x.Length));
    }

    [Fact]
    public void PickWeighted_ListDouble_NullItems_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickWeighted((IReadOnlyList<string>)null!, static x => (double)x.Length));
    }

    [Fact]
    public void PickWeighted_ListDouble_NullWeight_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickWeighted(Items, (Func<string, double>)null!));
    }

    [Fact]
    public void PickWeighted_ListDouble_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickWeighted((IReadOnlyList<string>)[], static x => (double)x.Length));
    }

    [Fact]
    public void PickWeighted_ListDouble_NegativeWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items, x => x == "b" ? -1.0 : 5.0));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_ListDouble_NaNWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items, x => x == "b" ? double.NaN : 5.0));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_ListDouble_PositiveInfinityWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items, x => x == "b" ? double.PositiveInfinity : 5.0));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_ListDouble_NegativeInfinityWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];

        Assert.Throws<ArgumentException>(() => random.PickWeighted(items, x => x == "b" ? double.NegativeInfinity : 5.0));
    }

    [Fact]
    public void PickWeighted_ListDouble_ZeroTotalWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickWeighted(Items, static _ => 0.0));
    }

    // ---- PickWeighted<T>(ReadOnlySpan<T>, ReadOnlySpan<long>) ----

    [Fact]
    public void PickWeighted_SpanLong_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.PickWeighted(Items.AsSpan(), LongWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanLong_MismatchedLengths_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        long[] weights = [1, 2];
        Assert.Throws<ArgumentException>(() => random.PickWeighted(Items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanLong_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickWeighted(ReadOnlySpan<string>.Empty, ReadOnlySpan<long>.Empty));
    }

    [Fact]
    public void PickWeighted_SpanLong_NegativeWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_SpanLong_ZeroTotalWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        long[] weights = [0, 0, 0];
        Assert.Throws<ArgumentException>(() => random.PickWeighted(Items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanLong_TotalOverflow_ThrowsOverflowException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [long.MaxValue, long.MaxValue];
        Assert.Throws<OverflowException>(() => random.PickWeighted(items.AsSpan(), weights.AsSpan()));
    }

    // ---- PickWeighted<T>(ReadOnlySpan<T>, ReadOnlySpan<double>) ----

    [Fact]
    public void PickWeighted_SpanDouble_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.PickWeighted(Items.AsSpan(), DoubleWeights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_MismatchedLengths_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        double[] weights = [1.0, 2.0];
        Assert.Throws<ArgumentException>(() => random.PickWeighted(Items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void PickWeighted_SpanDouble_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickWeighted(ReadOnlySpan<string>.Empty, ReadOnlySpan<double>.Empty));
    }

    [Fact]
    public void PickWeighted_SpanDouble_NegativeWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        double[] weights = [5.0, -1.0];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_SpanDouble_NaNWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        double[] weights = [5.0, double.NaN];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_SpanDouble_InfinityWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        double[] weights = [5.0, double.PositiveInfinity];

        var ex = Assert.Throws<ArgumentException>(() => random.PickWeighted(items.AsSpan(), weights.AsSpan()));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void PickWeighted_SpanDouble_ZeroTotalWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        double[] weights = [0.0, 0.0, 0.0];
        Assert.Throws<ArgumentException>(() => random.PickWeighted(Items.AsSpan(), weights.AsSpan()));
    }

    // ---- PickManyWeighted (with replacement) ----

    [Fact]
    public void PickManyWeighted_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.PickManyWeighted(Items, static x => (long)x.Length, 3));
    }

    [Fact]
    public void PickManyWeighted_NullItems_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickManyWeighted((IReadOnlyList<string>)null!, static x => (long)x.Length, 3));
    }

    [Fact]
    public void PickManyWeighted_NullWeight_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickManyWeighted(Items, (Func<string, long>)null!, 3));
    }

    [Fact]
    public void PickManyWeighted_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickManyWeighted((IReadOnlyList<string>)[], static x => (long)x.Length, 3));
    }

    [Fact]
    public void PickManyWeighted_NegativeWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        Assert.Throws<ArgumentException>(() => random.PickManyWeighted(items, x => weights[Array.IndexOf(items, x)], 1));
    }

    [Fact]
    public void PickManyWeighted_ZeroTotalWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickManyWeighted(Items, static _ => 0L, 3));
    }

    [Fact]
    public void PickManyWeighted_CountZero_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeighted(Items, static x => (long)x.Length, 0));
    }

    [Fact]
    public void PickManyWeighted_CountNegative_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeighted(Items, static x => (long)x.Length, -1));
    }

    [Fact]
    public void PickManyWeighted_TotalOverflow_ThrowsOverflowException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [long.MaxValue, long.MaxValue];

        Assert.Throws<OverflowException>(() => random.PickManyWeighted(items, x => weights[Array.IndexOf(items, x)], 1));
    }

    // ---- PickManyWeightedDistinct (without replacement) ----

    [Fact]
    public void PickManyWeightedDistinct_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.PickManyWeightedDistinct(Items, static x => (long)x.Length, 3));
    }

    [Fact]
    public void PickManyWeightedDistinct_NullItems_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickManyWeightedDistinct((IReadOnlyList<string>)null!, static x => (long)x.Length, 3));
    }

    [Fact]
    public void PickManyWeightedDistinct_NullWeight_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentNullException>(() => random.PickManyWeightedDistinct(Items, (Func<string, long>)null!, 3));
    }

    [Fact]
    public void PickManyWeightedDistinct_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickManyWeightedDistinct((IReadOnlyList<string>)[], static x => (long)x.Length, 3));
    }

    [Fact]
    public void PickManyWeightedDistinct_NegativeWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        Assert.Throws<ArgumentException>(() => random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 1));
    }

    [Fact]
    public void PickManyWeightedDistinct_ZeroTotalWeight_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentException>(() => random.PickManyWeightedDistinct(Items, static _ => 0L, 1));
    }

    [Fact]
    public void PickManyWeightedDistinct_CountZero_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeightedDistinct(Items, static x => (long)x.Length, 0));
    }

    [Fact]
    public void PickManyWeightedDistinct_CountExceedsPositiveWeightItemCount_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b", "c"];
        long[] weights = [1, 0, 1]; // only 2 items with positive weight

        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 3));
    }

    [Fact]
    public void PickManyWeightedDistinct_CountEqualsPositiveWeightItemCount_Succeeds()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b", "c"];
        long[] weights = [1, 0, 1];

        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 2);

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void PickManyWeightedDistinct_TotalOverflow_ThrowsOverflowException()
    {
        var random = new DeterministicRandom(1UL);
        string[] items = ["a", "b"];
        long[] weights = [long.MaxValue, long.MaxValue];

        Assert.Throws<OverflowException>(() => random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 1));
    }
}
