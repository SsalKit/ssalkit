using System.Collections.Immutable;
using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// The generated extensions take an <see cref="IReadOnlyList{T}"/> receiver, which is what makes the
/// three collection shapes a caller realistically holds -- <see cref="List{T}"/>, an array, and
/// <see cref="ImmutableArray{T}"/> -- all work without an intermediate copy.
/// </summary>
/// <remarks>
/// <see cref="ImmutableArray{T}"/> is the interesting one: it is a struct, so the call site relies on
/// the boxing conversion an extension-method receiver permits. That it compiles at all is the
/// assertion; the equality checks below just confirm all three shapes see the same items.
/// </remarks>
public class ReceiverTypeTests
{
    private const ulong Seed = 2026;

    [Fact]
    public void ListArrayAndImmutableArrayReceivers_AllPickTheSameItem()
    {
        List<LootEntry> list = WeightedTables.Loot();
        LootEntry[] array = [.. list];
        ImmutableArray<LootEntry> immutable = [.. list];
        IReadOnlyList<LootEntry> asInterface = list;

        LootEntry fromList = list.PickWeighted(new DeterministicRandom(Seed));
        LootEntry fromArray = array.PickWeighted(new DeterministicRandom(Seed));
        LootEntry fromImmutable = immutable.PickWeighted(new DeterministicRandom(Seed));
        LootEntry fromInterface = asInterface.PickWeighted(new DeterministicRandom(Seed));

        Assert.Same(fromList, fromArray);
        Assert.Same(fromList, fromImmutable);
        Assert.Same(fromList, fromInterface);
    }

    [Fact]
    public void ArrayReceiver_SupportsTheWholeGeneratedSurface()
    {
        LootEntry[] array = [.. WeightedTables.Loot()];

        Assert.NotNull(array.PickWeighted(new DeterministicRandom(Seed)));
        Assert.Equal(3, array.PickManyWeighted(new DeterministicRandom(Seed), 3).Length);
        Assert.Equal(2, array.PickManyWeightedDistinct(new DeterministicRandom(Seed), 2).Length);
        Assert.Equal(array.Length, array.ToWeightedSampler().Count);
    }

    [Fact]
    public void ImmutableArrayReceiver_SupportsTheWholeGeneratedSurface()
    {
        ImmutableArray<LootEntry> immutable = [.. WeightedTables.Loot()];

        Assert.NotNull(immutable.PickWeighted(new DeterministicRandom(Seed)));
        Assert.Equal(3, immutable.PickManyWeighted(new DeterministicRandom(Seed), 3).Length);
        Assert.Equal(2, immutable.PickManyWeightedDistinct(new DeterministicRandom(Seed), 2).Length);
        Assert.Equal(immutable.Length, immutable.ToWeightedSampler().Count);
    }

    /// <summary>
    /// An <see langword="internal"/> model type: its extension class is internal, so this call
    /// compiles here (same assembly) and would not from outside.
    /// </summary>
    [Fact]
    public void InternalModel_GeneratedExtensionsAreUsableWithinTheAssembly()
    {
        List<InternalWeightedItem> items =
        [
            new() { Name = "a", Weight = 3 },
            new() { Name = "b", Weight = 1 },
        ];

        InternalWeightedItem picked = items.PickWeighted(new DeterministicRandom(Seed));

        Assert.Contains(picked, items);
    }

    /// <summary>
    /// The nested model type, called through its flattened extension class.
    /// </summary>
    [Fact]
    public void NestedModel_GeneratedExtensionsAreUsable()
    {
        List<WeightedContainer.NestedItem> items =
        [
            new() { Name = "a", Weight = 3 },
            new() { Name = "b", Weight = 1 },
        ];

        WeightedContainer.NestedItem picked = items.PickWeighted(new DeterministicRandom(Seed));

        Assert.Contains(picked, items);
    }

    /// <summary>
    /// The <c>InternalExtensions = true</c> model, exercised the same way.
    /// </summary>
    [Fact]
    public void ForcedInternalModel_GeneratedExtensionsAreUsableWithinTheAssembly()
    {
        List<ForcedInternalItem> items =
        [
            new() { Name = "a", Weight = 3 },
            new() { Name = "b", Weight = 1 },
        ];

        ForcedInternalItem picked = items.PickWeighted(new DeterministicRandom(Seed));

        Assert.Contains(picked, items);
    }
}
