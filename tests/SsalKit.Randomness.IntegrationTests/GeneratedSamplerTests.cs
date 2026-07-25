using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// Verifies the generated <c>ToWeightedSampler()</c> against a sampler built the long way with
/// <see cref="WeightedSampler{T}.Create(IReadOnlyList{T}, Func{T, long})"/>.
/// </summary>
/// <remarks>
/// Alias-table construction is order-sensitive, so two samplers that draw the same items for the
/// same seed are also evidence that the generated call passed the items and weights through
/// unchanged rather than, say, in a re-sorted order.
/// </remarks>
public class GeneratedSamplerTests
{
    private const int Draws = 200;

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void ToWeightedSampler_DrawsSameSequenceAsManuallyBuiltSampler(ulong seed)
    {
        var items = WeightedTables.Loot();

        WeightedSampler<LootEntry> generated = items.ToWeightedSampler();
        WeightedSampler<LootEntry> expected = WeightedSampler<LootEntry>.Create(items, WeightedTables.LootWeight);

        var generatedRng = new DeterministicRandom(seed);
        var expectedRng = new DeterministicRandom(seed);

        Assert.Equal(expected.Count, generated.Count);
        for (int i = 0; i < Draws; i++)
        {
            Assert.Same(expected.Pick(expectedRng), generated.Pick(generatedRng));
        }

        Assert.Equal(expectedRng.ExportState(), generatedRng.ExportState());
    }

    /// <summary>
    /// The same check for a narrower integral weight, which reaches the runtime through the
    /// generated <c>(long)</c> cast.
    /// </summary>
    [Fact]
    public void ToWeightedSampler_IntWeight_DrawsSameSequenceAsManuallyBuiltSampler()
    {
        var items = WeightedTables.Ints();

        IntWeightedItem[] generated = items.ToWeightedSampler().PickMany(new DeterministicRandom(42), Draws);
        IntWeightedItem[] expected = WeightedSampler<IntWeightedItem>
            .Create(items, WeightedTables.IntWeight)
            .PickMany(new DeterministicRandom(42), Draws);

        Assert.Equal(expected.Length, generated.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Same(expected[i], generated[i]);
        }
    }

    /// <summary>
    /// The build-once, draw-many usage the generated XML docs steer callers toward: one sampler,
    /// many draws, each draw taking the source explicitly.
    /// </summary>
    [Fact]
    public void ToWeightedSampler_BuiltOnce_IsReusableAcrossSources()
    {
        WeightedSampler<LootEntry> sampler = WeightedTables.Loot().ToWeightedSampler();

        LootEntry fromFirstSource = sampler.Pick(new DeterministicRandom(7));
        LootEntry fromSecondSource = sampler.Pick(new DeterministicRandom(7));

        // The same sampler, handed two independent sources in the same state, draws the same item.
        Assert.Same(fromFirstSource, fromSecondSource);

        // Reusing it for a long run still honours the weights: the zero-weight entry never appears.
        LootEntry[] longRun = sampler.PickMany(new DeterministicRandom(9), 1_000);
        Assert.All(longRun, entry => Assert.NotEqual("unobtainable", entry.ItemId));
        Assert.Contains(longRun, entry => entry.ItemId == "common");
    }
}
