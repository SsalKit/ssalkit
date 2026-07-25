using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// The item tables and the hand-written selectors the generated extensions are compared against.
/// </summary>
/// <remarks>
/// Each selector here is exactly what the generator writes into its delegating call
/// (<c>static x =&gt; (long)x.Weight</c>), so a difference in results can only come from the
/// generated code calling a different overload, in a different order, or with different arguments.
/// </remarks>
public static class WeightedTables
{
    /// <summary>Seeds used by every parity theory, so a coincidental match on one seed cannot pass.</summary>
    public static TheoryData<ulong> Seeds => [1UL, 42UL, 777UL, 0xDEAD_BEEFUL, ulong.MaxValue];

    public static readonly Func<LootEntry, long> LootWeight = static x => (long)x.Weight;

    public static readonly Func<IntWeightedItem, long> IntWeight = static x => (long)x.Weight;

    public static readonly Func<FieldWeightedItem, long> FieldWeight = static x => (long)x.Weight;

    public static readonly Func<DoubleWeightedItem, double> DoubleWeight = static x => (double)x.Weight;

    public static readonly Func<FloatWeightedItem, double> FloatWeight = static x => (double)x.Weight;

    public static readonly Func<BaseLootEntry, long> BaseWeight = static x => (long)x.Weight;

    /// <summary>
    /// A gacha-style table with a wide weight spread plus one zero-weight entry, so the picks
    /// exercise more than one bucket of the cumulative array.
    /// </summary>
    public static List<LootEntry> Loot() =>
    [
        new() { ItemId = "common", Weight = 60 },
        new() { ItemId = "uncommon", Weight = 30 },
        new() { ItemId = "rare", Weight = 9 },
        new() { ItemId = "legendary", Weight = 1 },
        new() { ItemId = "unobtainable", Weight = 0 },
    ];

    public static List<IntWeightedItem> Ints() =>
    [
        new() { Name = "a", Weight = 5 },
        new() { Name = "b", Weight = 3 },
        new() { Name = "c", Weight = 2 },
    ];

    public static List<FieldWeightedItem> Fields() =>
    [
        new("a", 7),
        new("b", 2),
        new("c", 1),
    ];

    public static List<DoubleWeightedItem> Doubles() =>
    [
        new() { Name = "a", Weight = 0.5 },
        new() { Name = "b", Weight = 0.25 },
        new() { Name = "c", Weight = 0.25 },
    ];

    public static List<FloatWeightedItem> Floats() =>
    [
        new() { Name = "a", Weight = 1.5f },
        new() { Name = "b", Weight = 0.5f },
    ];

    public static List<SharedSourceItem> SharedSource() =>
    [
        new() { Name = "a", Weight = 6 },
        new() { Name = "b", Weight = 3 },
        new() { Name = "c", Weight = 1 },
    ];

    public static List<SharedSourceDoubleItem> SharedSourceDoubles() =>
    [
        new() { Name = "a", Weight = 0.75 },
        new() { Name = "b", Weight = 0.25 },
    ];

    public static List<SharedSourceInternalItem> SharedSourceInternal() =>
    [
        new() { Name = "a", Weight = 2 },
        new() { Name = "b", Weight = 1 },
    ];

    public static List<DerivedLootEntry> Derived() =>
    [
        new() { ItemId = "sword", Rarity = "common", Weight = 4 },
        new() { ItemId = "shield", Rarity = "rare", Weight = 3 },
        new() { ItemId = "crown", Rarity = "legendary", Weight = 1 },
    ];
}
