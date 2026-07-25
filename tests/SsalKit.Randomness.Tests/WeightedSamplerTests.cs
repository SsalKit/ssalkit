namespace SsalKit.Randomness.Tests;

/// <summary>
/// Covers <see cref="WeightedSampler{T}"/>: the shared exception contract on <c>Create</c>, the
/// <see cref="WeightedSampler{T}.Count"/> property, immutability/no cross-contamination when a
/// single instance is driven by independent <see cref="IRandomSource"/>s, and the single-item
/// edge case.
/// </summary>
public class WeightedSamplerTests
{
    private static readonly string[] Items = ["a", "b", "c"];
    private static readonly long[] Weights = [1, 2, 3];

    // ---- Create(IReadOnlyList<T>, Func<T, long>) exception contract ----

    [Fact]
    public void Create_ListLong_NullItems_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => WeightedSampler<string>.Create((IReadOnlyList<string>)null!, static x => (long)x.Length));
    }

    [Fact]
    public void Create_ListLong_NullWeight_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => WeightedSampler<string>.Create(Items, (Func<string, long>)null!));
    }

    [Fact]
    public void Create_ListLong_EmptyItems_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create((IReadOnlyList<string>)[], static x => (long)x.Length));
    }

    [Fact]
    public void Create_ListLong_NegativeWeight_ThrowsArgumentException()
    {
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        var ex = Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create(items, x => weights[Array.IndexOf(items, x)]));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void Create_ListLong_ZeroTotalWeight_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create(Items, static _ => 0L));
    }

    [Fact]
    public void Create_ListLong_TotalOverflow_ThrowsOverflowException()
    {
        string[] items = ["a", "b"];
        long[] weights = [long.MaxValue, long.MaxValue];

        Assert.Throws<OverflowException>(() => WeightedSampler<string>.Create(items, x => weights[Array.IndexOf(items, x)]));
    }

    // ---- Create(ReadOnlySpan<T>, ReadOnlySpan<long>) exception contract ----

    [Fact]
    public void Create_SpanLong_MismatchedLengths_ThrowsArgumentException()
    {
        long[] weights = [1, 2];
        Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create(Items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void Create_SpanLong_EmptyItems_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create(ReadOnlySpan<string>.Empty, ReadOnlySpan<long>.Empty));
    }

    [Fact]
    public void Create_SpanLong_NegativeWeight_ThrowsArgumentException()
    {
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        var ex = Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create(items.AsSpan(), weights.AsSpan()));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void Create_SpanLong_ZeroTotalWeight_ThrowsArgumentException()
    {
        long[] weights = [0, 0, 0];
        Assert.Throws<ArgumentException>(() => WeightedSampler<string>.Create(Items.AsSpan(), weights.AsSpan()));
    }

    [Fact]
    public void Create_SpanLong_TotalOverflow_ThrowsOverflowException()
    {
        string[] items = ["a", "b"];
        long[] weights = [long.MaxValue, long.MaxValue];
        Assert.Throws<OverflowException>(() => WeightedSampler<string>.Create(items.AsSpan(), weights.AsSpan()));
    }

    // ---- Pick / PickMany argument checks ----

    [Fact]
    public void Pick_NullSource_ThrowsArgumentNullException()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);
        Assert.Throws<ArgumentNullException>(() => sampler.Pick(null!));
    }

    [Fact]
    public void PickMany_NullSource_ThrowsArgumentNullException()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);
        Assert.Throws<ArgumentNullException>(() => sampler.PickMany(null!, 3));
    }

    [Fact]
    public void PickMany_CountZero_ThrowsArgumentOutOfRangeException()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.PickMany(random, 0));
    }

    [Fact]
    public void PickMany_CountNegative_ThrowsArgumentOutOfRangeException()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.PickMany(random, -5));
    }

    // ---- Count ----

    [Fact]
    public void Count_ReflectsNumberOfItemsSamplerWasBuiltFrom()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);
        Assert.Equal(3, sampler.Count);
    }

    // ---- Single-item edge case ----

    [Fact]
    public void Pick_SingleItem_AlwaysReturnsIt()
    {
        var sampler = WeightedSampler<string>.Create(["only"], static _ => 1L);
        var random = new DeterministicRandom(1UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal("only", sampler.Pick(random));
        }
    }

    [Fact]
    public void Pick_SingleItem_LargeWeight_AlwaysReturnsIt()
    {
        var sampler = WeightedSampler<string>.Create(["only"], static _ => 1_000_000L);
        var random = new DeterministicRandom(1UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal("only", sampler.Pick(random));
        }
    }

    // ---- Zero-weight item is never selected ----

    [Fact]
    public void Pick_ZeroWeightItem_IsNeverSelectedOverManyDraws()
    {
        string[] items = ["a", "z", "b"];
        long[] weights = [10, 0, 10];
        var sampler = WeightedSampler<string>.Create(items, x => weights[Array.IndexOf(items, x)]);

        var random = new DeterministicRandom(2024UL);
        for (int i = 0; i < 5_000; i++)
        {
            Assert.NotEqual("z", sampler.Pick(random));
        }
    }

    // ---- Immutability / no cross-contamination across independent sources ----

    [Fact]
    public void Pick_SameSamplerInstance_TwoIndependentSources_ProduceIndependentReproducibleSequences()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);

        var sourceA1 = new DeterministicRandom(111UL);
        var sourceB1 = new DeterministicRandom(222UL);
        var sourceA2 = new DeterministicRandom(111UL);
        var sourceB2 = new DeterministicRandom(222UL);

        var resultsA1 = new string[50];
        var resultsB1 = new string[50];

        // Interleave draws against the two sources through the same sampler instance to prove
        // there is no shared mutable draw-time state.
        for (int i = 0; i < 50; i++)
        {
            resultsA1[i] = sampler.Pick(sourceA1);
            resultsB1[i] = sampler.Pick(sourceB1);
        }

        var resultsA2 = sampler.PickMany(sourceA2, 50);
        var resultsB2 = sampler.PickMany(sourceB2, 50);

        Assert.Equal(resultsA1, resultsA2);
        Assert.Equal(resultsB1, resultsB2);
    }

    [Fact]
    public void Pick_SameSamplerInstance_ConcurrentDrawsFromIndependentSources_DoNotThrowOrCorrupt()
    {
        var sampler = WeightedSampler<string>.Create(Items, static x => (long)x.Length);

        var results = new string[8][];
        System.Threading.Tasks.Parallel.For(0, 8, i =>
        {
            var random = new DeterministicRandom((ulong)(1000 + i));
            results[i] = sampler.PickMany(random, 200);
        });

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(200, results[i].Length);
            foreach (string item in results[i])
            {
                Assert.Contains(item, Items);
            }
        }
    }
}
