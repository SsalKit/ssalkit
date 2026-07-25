namespace SsalKit.Randomness.Tests;

/// <summary>
/// Covers <see cref="WeightedRandomExtensions.ToWeightedSampler{T}"/>: that it is a pure delegation
/// to <see cref="WeightedSampler{T}.Create(IReadOnlyList{T}, Func{T, long})"/> — same sampler, same
/// draw sequence, same exception contract — and that the element type is inferred from the receiver.
/// </summary>
public class ToWeightedSamplerTests
{
    private static readonly string[] Items = ["a", "b", "c", "d"];

    private static long LengthWeight(string item) => item.Length;

    private static long IndexWeight(string item) => Array.IndexOf(Items, item) + 1;

    [Fact]
    public void ToWeightedSampler_BuildsSamplerOverAllItems()
    {
        var sampler = Items.ToWeightedSampler(IndexWeight);

        Assert.Equal(Items.Length, sampler.Count);
    }

    [Fact]
    public void ToWeightedSampler_ProducesSameDrawSequenceAsCreate()
    {
        var viaExtension = Items.ToWeightedSampler(IndexWeight);
        var viaCreate = WeightedSampler<string>.Create(Items, IndexWeight);

        var randomA = new DeterministicRandom(20260725UL);
        var randomB = new DeterministicRandom(20260725UL);

        string[] fromExtension = viaExtension.PickMany(randomA, 200);
        string[] fromCreate = viaCreate.PickMany(randomB, 200);

        Assert.Equal(fromCreate, fromExtension);
    }

    [Fact]
    public void ToWeightedSampler_InfersElementTypeFromReceiver()
    {
        // The point of the extension over the static factory: no explicit type argument. If
        // inference ever regressed, this would not compile.
        IReadOnlyList<int> numbers = [10, 20, 30];

        WeightedSampler<int> sampler = numbers.ToWeightedSampler(static x => (long)x);

        Assert.Equal(3, sampler.Count);
    }

    [Fact]
    public void ToWeightedSampler_ZeroWeightItem_IsNeverDrawn()
    {
        string[] items = ["kept", "never", "kept2"];
        long[] weights = [5, 0, 5];
        var sampler = items.ToWeightedSampler(x => weights[Array.IndexOf(items, x)]);

        var random = new DeterministicRandom(7UL);
        foreach (string drawn in sampler.PickMany(random, 2_000))
        {
            Assert.NotEqual("never", drawn);
        }
    }

    // ---- Exception contract: forwarded verbatim from Create ----

    [Fact]
    public void ToWeightedSampler_NullItems_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyList<string>)null!).ToWeightedSampler(LengthWeight));
    }

    [Fact]
    public void ToWeightedSampler_NullWeight_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Items.ToWeightedSampler(null!));
    }

    [Fact]
    public void ToWeightedSampler_EmptyItems_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ((IReadOnlyList<string>)[]).ToWeightedSampler(LengthWeight));
    }

    [Fact]
    public void ToWeightedSampler_NegativeWeight_ThrowsArgumentException()
    {
        string[] items = ["a", "b"];
        long[] weights = [5, -1];

        var ex = Assert.Throws<ArgumentException>(() => items.ToWeightedSampler(x => weights[Array.IndexOf(items, x)]));
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void ToWeightedSampler_ZeroTotalWeight_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Items.ToWeightedSampler(static _ => 0L));
    }

    [Fact]
    public void ToWeightedSampler_TotalOverflow_ThrowsOverflowException()
    {
        string[] items = ["a", "b"];

        Assert.Throws<OverflowException>(() => items.ToWeightedSampler(static _ => long.MaxValue));
    }
}
