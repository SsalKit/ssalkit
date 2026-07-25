using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// Pins down the documented consequence of the design's inheritance non-goal: <c>[RandomWeight]</c>
/// on a base type generates extensions for the <em>base</em> type only.
/// </summary>
/// <remarks>
/// A <c>List&lt;Derived&gt;</c> can still call them, because <see cref="IReadOnlyList{T}"/> is
/// covariant in its element type -- but the call binds to the base type's extension, so the result is
/// statically a <c>Base</c> and the caller has to narrow it. That is a trade the design accepts
/// rather than a defect; this test exists so the behaviour cannot change silently.
/// </remarks>
public class InheritanceTests
{
    private const ulong Seed = 4242;

    [Fact]
    public void DerivedList_UsesTheBaseTypesGeneratedExtension_AndYieldsAStaticallyBaseTypedResult()
    {
        List<DerivedLootEntry> derived = WeightedTables.Derived();

        var picked = derived.PickWeighted(new DeterministicRandom(Seed));

        // The *static* type of the result is the base type, even though the list held derived items.
        Assert.Equal(typeof(BaseLootEntry), StaticTypeOf(picked));

        // The instance itself is of course still the derived one that was in the list.
        Assert.IsType<DerivedLootEntry>(picked);
        Assert.Contains(picked, derived);

        static Type StaticTypeOf<T>(T value) => typeof(T);
    }

    /// <summary>
    /// No extension class is generated for the derived type: the attribute is not inherited, and the
    /// generator does not walk base types looking for one.
    /// </summary>
    [Fact]
    public void DerivedType_GetsNoExtensionClassOfItsOwn()
    {
        var assembly = typeof(DerivedLootEntry).Assembly;
        var @namespace = typeof(DerivedLootEntry).Namespace;

        Assert.NotNull(assembly.GetType(@namespace + ".BaseLootEntryRandomWeightExtensions"));
        Assert.Null(assembly.GetType(@namespace + ".DerivedLootEntryRandomWeightExtensions"));
    }

    /// <summary>
    /// The delegation contract still holds through the covariant receiver: the derived list picks the
    /// same items, in the same order, as a hand-written selector call over the same list.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeightedTables.Seeds), MemberType = typeof(WeightedTables))]
    public void DerivedList_MatchesTheSelectorOverload(ulong seed)
    {
        List<DerivedLootEntry> derived = WeightedTables.Derived();
        var generatedRng = new DeterministicRandom(seed);
        var selectorRng = new DeterministicRandom(seed);

        for (int i = 0; i < 50; i++)
        {
            BaseLootEntry generated = derived.PickWeighted(generatedRng);
            BaseLootEntry expected = selectorRng.PickWeighted<BaseLootEntry>(derived, WeightedTables.BaseWeight);

            Assert.Same(expected, generated);
        }

        Assert.Equal(selectorRng.ExportState(), generatedRng.ExportState());
    }
}
