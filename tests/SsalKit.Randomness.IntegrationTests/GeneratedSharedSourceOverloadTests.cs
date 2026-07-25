using System.Reflection;
using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// The <c>[RandomWeight(SharedSourceOverloads = true)]</c> opt-in, end to end: the argument-less
/// overloads exist, draw real items from <see cref="SharedRandomSource.Instance"/>, sit next to the
/// source-taking forms rather than replacing them, and are absent entirely from a type that did not
/// ask for them.
/// </summary>
/// <remarks>
/// Draws from the shared source cannot be compared against a fixed seed, so what these assert is the
/// invariant that holds for <em>any</em> source: every returned item comes from the table, batched
/// draws have the requested length, and distinct draws do not repeat. The exact-parity tests against
/// a hand-written selector live in <see cref="GeneratedPickParityTests"/>, on the deterministic
/// overloads the argument-less ones delegate to.
/// </remarks>
public class GeneratedSharedSourceOverloadTests
{
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private const int Draws = 500;

    [Fact]
    public void ArgumentLessPickWeighted_ReturnsItemsFromTheTable()
    {
        var items = WeightedTables.SharedSource();

        for (int i = 0; i < Draws; i++)
        {
            SharedSourceItem drawn = items.PickWeighted();

            Assert.Contains(drawn, items);
        }
    }

    /// <summary>
    /// Every entry of the table is reachable, which is what proves the draw actually varies rather
    /// than returning a constant. The lightest entry carries a tenth of the weight, so over 500
    /// draws the chance of never seeing it is around 1e-23 -- far below any realistic flake budget.
    /// </summary>
    [Fact]
    public void ArgumentLessPickWeighted_ReachesEveryEntryOverManyDraws()
    {
        var items = WeightedTables.SharedSource();
        var seen = new HashSet<string>();

        for (int i = 0; i < Draws; i++)
        {
            seen.Add(items.PickWeighted().Name);
        }

        Assert.Equal(items.Select(item => item.Name).ToHashSet(), seen);
    }

    [Fact]
    public void ArgumentLessPickManyWeighted_ReturnsTheRequestedCount()
    {
        var items = WeightedTables.SharedSource();

        SharedSourceItem[] drawn = items.PickManyWeighted(count: 10);

        Assert.Equal(10, drawn.Length);
        Assert.All(drawn, item => Assert.Contains(item, items));
    }

    [Fact]
    public void ArgumentLessPickManyWeightedDistinct_ReturnsDistinctItems()
    {
        var items = WeightedTables.SharedSource();

        SharedSourceItem[] drawn = items.PickManyWeightedDistinct(count: 3);

        Assert.Equal(3, drawn.Length);
        Assert.Equal(3, drawn.Distinct().Count());
        Assert.All(drawn, item => Assert.Contains(item, items));
    }

    [Fact]
    public void ArgumentLessPickWeighted_OnADoubleWeight_ReturnsItemsFromTheTable()
    {
        var items = WeightedTables.SharedSourceDoubles();

        for (int i = 0; i < 100; i++)
        {
            Assert.Contains(items.PickWeighted(), items);
        }
    }

    /// <summary>
    /// The opt-in adds overloads; it never removes the ones that keep a draw reproducible. Both
    /// forms have to be callable on the same table, with the explicit one still seedable.
    /// </summary>
    [Fact]
    public void ArgumentLessAndExplicitSourceOverloads_Coexist()
    {
        var items = WeightedTables.SharedSource();
        var seeded = new DeterministicRandom(seed: 42);
        var replay = new DeterministicRandom(seed: 42);

        SharedSourceItem fromShared = items.PickWeighted();
        SharedSourceItem fromSeeded = items.PickWeighted(seeded);
        SharedSourceItem[] batchedFromShared = items.PickManyWeighted(count: 4);
        SharedSourceItem[] batchedFromSeeded = items.PickManyWeighted(seeded, count: 4);

        Assert.Contains(fromShared, items);
        Assert.Contains(fromSeeded, items);
        Assert.Equal(4, batchedFromShared.Length);

        // The explicit overload is untouched by the opt-in: the same seed replays it exactly.
        Assert.Same(fromSeeded, items.PickWeighted(replay));
        Assert.Equal(batchedFromSeeded, items.PickManyWeighted(replay, count: 4));
    }

    /// <summary>
    /// The argument-less form validates nothing of its own -- it hands the shared source to the
    /// explicit overload, so the runtime's exception contract reaches the caller unchanged.
    /// </summary>
    [Fact]
    public void ArgumentLessOverloads_ShareTheExceptionContract()
    {
        List<SharedSourceItem> empty = [];

        var single = Assert.Throws<ArgumentException>(() => empty.PickWeighted());
        var batched = Assert.Throws<ArgumentException>(() => empty.PickManyWeighted(count: 2));
        var distinct = Assert.Throws<ArgumentException>(() => empty.PickManyWeightedDistinct(count: 2));

        Assert.Equal("items", single.ParamName);
        Assert.Equal("items", batched.ParamName);
        Assert.Equal("items", distinct.ParamName);
    }

    /// <summary>
    /// <c>ToWeightedSampler()</c> never took a source, so the opt-in leaves it exactly as it was --
    /// one method, not two.
    /// </summary>
    [Fact]
    public void ToWeightedSampler_IsUnaffectedByTheOptIn()
    {
        var items = WeightedTables.SharedSource();

        WeightedSampler<SharedSourceItem> sampler = items.ToWeightedSampler();

        Assert.Contains(sampler.Pick(SharedRandomSource.Instance), items);
        Assert.Equal(1, CountOverloads(typeof(SharedSourceItemRandomWeightExtensions), "ToWeightedSampler"));
    }

    [Fact]
    public void OptedInIntegralType_GeneratesSevenMethods()
    {
        var extensions = typeof(SharedSourceItemRandomWeightExtensions);

        Assert.Equal(2, CountOverloads(extensions, "PickWeighted"));
        Assert.Equal(2, CountOverloads(extensions, "PickManyWeighted"));
        Assert.Equal(2, CountOverloads(extensions, "PickManyWeightedDistinct"));
        Assert.Equal(1, CountOverloads(extensions, "ToWeightedSampler"));
    }

    /// <summary>
    /// The opt-in does not widen the floating-point matrix: a <see cref="double"/> weight still
    /// mirrors the runtime surface, which has no batched or alias-table API for double weights.
    /// </summary>
    [Fact]
    public void OptedInDoubleType_GeneratesBothPickWeightedFormsAndNothingElse()
    {
        var extensions = typeof(SharedSourceDoubleItemRandomWeightExtensions);

        Assert.Equal(2, CountOverloads(extensions, "PickWeighted"));
        Assert.Equal(0, CountOverloads(extensions, "PickManyWeighted"));
        Assert.Equal(0, CountOverloads(extensions, "PickManyWeightedDistinct"));
        Assert.Equal(0, CountOverloads(extensions, "ToWeightedSampler"));
    }

    /// <summary>
    /// Reflection is the only way to assert something was <em>not</em> generated: a type that never
    /// opted in has no argument-less overload, so the source of a draw stays visible at every call
    /// site -- which is the default the opt-in exists to preserve.
    /// </summary>
    [Fact]
    public void TypeWithoutTheOptIn_HasNoArgumentLessOverloads()
    {
        var extensions = typeof(LootEntryRandomWeightExtensions);

        Assert.Equal(1, CountOverloads(extensions, "PickWeighted"));
        Assert.Equal(1, CountOverloads(extensions, "PickManyWeighted"));
        Assert.Equal(1, CountOverloads(extensions, "PickManyWeightedDistinct"));

        Assert.All(
            extensions.GetMethods(AnyStatic).Where(method => method.Name.StartsWith("Pick", StringComparison.Ordinal)),
            method => Assert.Contains(method.GetParameters(), p => p.ParameterType == typeof(IRandomSource)));
    }

    /// <summary>
    /// The two options are orthogonal: <c>InternalExtensions</c> decides the class's visibility and
    /// <c>SharedSourceOverloads</c> decides its method set, and setting both applies both.
    /// </summary>
    [Fact]
    public void InternalExtensionsAndSharedSourceOverloads_CombineIndependently()
    {
        var extensions = typeof(SharedSourceInternalItemRandomWeightExtensions);

        Assert.True(typeof(SharedSourceInternalItem).IsPublic);
        Assert.True(extensions.IsNotPublic);
        Assert.Equal(2, CountOverloads(extensions, "PickWeighted"));

        var items = WeightedTables.SharedSourceInternal();
        Assert.Contains(items.PickWeighted(), items);
    }

    private static int CountOverloads(Type extensions, string methodName) =>
        extensions.GetMethods(AnyStatic).Count(method => method.Name == methodName);
}
