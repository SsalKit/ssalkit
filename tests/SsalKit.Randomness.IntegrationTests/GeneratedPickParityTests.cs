using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// The central contract of the <c>[RandomWeight]</c> generator: a generated extension is a pure
/// delegation to the selector-based runtime overload, so for the same starting state the two must
/// return the same items <em>and</em> leave the random source in the same state.
/// </summary>
/// <remarks>
/// Comparing the exported <see cref="RandomState"/> afterwards is what makes these tests
/// sensitive to a generated method that produced the right answer by drawing a different number of
/// values (or drawing them in a different order) -- something a result-only comparison over a small
/// table could easily miss.
/// </remarks>
public class GeneratedPickParityTests
{
    private const int Draws = 100;

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickWeighted_Long_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Loot();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        for (int i = 0; i < Draws; i++)
        {
            LootEntry generated = items.PickWeighted(generatedRng);
            LootEntry expected = selectorRng.PickWeighted(items, WeightedTables.LootWeight);

            Assert.Same(expected, generated);
        }

        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickWeighted_Int_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Ints();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        for (int i = 0; i < Draws; i++)
        {
            Assert.Same(selectorRng.PickWeighted(items, WeightedTables.IntWeight), items.PickWeighted(generatedRng));
        }

        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickWeighted_Field_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Fields();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        for (int i = 0; i < Draws; i++)
        {
            Assert.Same(selectorRng.PickWeighted(items, WeightedTables.FieldWeight), items.PickWeighted(generatedRng));
        }

        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickWeighted_Double_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Doubles();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        for (int i = 0; i < Draws; i++)
        {
            Assert.Same(selectorRng.PickWeighted(items, WeightedTables.DoubleWeight), items.PickWeighted(generatedRng));
        }

        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickWeighted_Float_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Floats();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        for (int i = 0; i < Draws; i++)
        {
            Assert.Same(selectorRng.PickWeighted(items, WeightedTables.FloatWeight), items.PickWeighted(generatedRng));
        }

        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickManyWeighted_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Loot();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        LootEntry[] generated = items.PickManyWeighted(generatedRng, Draws);
        LootEntry[] expected = selectorRng.PickManyWeighted(items, WeightedTables.LootWeight, Draws);

        AssertSameSequence(expected, generated);
        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void PickManyWeightedDistinct_MatchesSelectorOverload(ulong seed)
    {
        var items = WeightedTables.Loot();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        // The table has four strictly positive weights, which is the ceiling for a distinct draw.
        LootEntry[] generated = items.PickManyWeightedDistinct(generatedRng, 4);
        LootEntry[] expected = selectorRng.PickManyWeightedDistinct(items, WeightedTables.LootWeight, 4);

        AssertSameSequence(expected, generated);
        Assert.Equal(4, generated.Distinct().Count());
        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }

    /// <summary>
    /// Confirms the parity above is not vacuous: different seeds really do drive different picks,
    /// so a generated method that ignored its <c>source</c> argument entirely could not pass.
    /// </summary>
    [Fact]
    public void PickManyWeighted_DiffersAcrossSeeds()
    {
        var items = WeightedTables.Loot();

        LootEntry[] first = items.PickManyWeighted(new DeterministicRandom(1), Draws);
        LootEntry[] second = items.PickManyWeighted(new DeterministicRandom(2), Draws);

        Assert.NotEqual(first.Select(x => x.ItemId), second.Select(x => x.ItemId));
    }

    /// <summary>
    /// Compares two draw sequences by reference identity rather than by value: the tables here hold
    /// distinct instances, so identity pins down <em>which</em> element was drawn, not merely one
    /// that happens to look the same.
    /// </summary>
    private static void AssertSameSequence<T>(T[] expected, T[] actual)
        where T : class
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Same(expected[i], actual[i]);
        }
    }
}
