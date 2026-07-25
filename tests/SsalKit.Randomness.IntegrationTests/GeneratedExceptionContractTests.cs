using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// The generated extensions add no validation of their own, so every argument-contract violation has
/// to surface as the exact exception the selector-based runtime overload throws -- same type, same
/// message, same <see cref="ArgumentException.ParamName"/>.
/// </summary>
/// <remarks>
/// The <c>ParamName</c> comparison is the load-bearing part: it is what would break if a generated
/// method ever wrapped, re-threw, or re-validated instead of delegating straight through.
/// </remarks>
public class GeneratedExceptionContractTests
{
    private static readonly List<LootEntry> Empty = [];

    private static readonly List<LootEntry> AllZero =
    [
        new() { ItemId = "a", Weight = 0 },
        new() { ItemId = "b", Weight = 0 },
    ];

    private static readonly List<LootEntry> Negative =
    [
        new() { ItemId = "a", Weight = 5 },
        new() { ItemId = "b", Weight = -1 },
    ];

    public static TheoryData<string> InvalidTables => ["empty", "all-zero", "negative"];

    private static List<LootEntry> TableFor(string name) => name switch
    {
        "empty" => Empty,
        "all-zero" => AllZero,
        _ => Negative,
    };

    [Theory]
    [MemberData(nameof(InvalidTables))]
    public void PickWeighted_ThrowsSameExceptionAsSelectorOverload(string table)
    {
        var items = TableFor(table);

        AssertSameFailure(
            () => new DeterministicRandom(1).PickWeighted(items, WeightedTables.LootWeight),
            () => items.PickWeighted(new DeterministicRandom(1)));
    }

    [Theory]
    [MemberData(nameof(InvalidTables))]
    public void PickManyWeighted_ThrowsSameExceptionAsSelectorOverload(string table)
    {
        var items = TableFor(table);

        AssertSameFailure(
            () => new DeterministicRandom(1).PickManyWeighted(items, WeightedTables.LootWeight, 3),
            () => items.PickManyWeighted(new DeterministicRandom(1), 3));
    }

    [Theory]
    [MemberData(nameof(InvalidTables))]
    public void PickManyWeightedDistinct_ThrowsSameExceptionAsSelectorOverload(string table)
    {
        var items = TableFor(table);

        AssertSameFailure(
            () => new DeterministicRandom(1).PickManyWeightedDistinct(items, WeightedTables.LootWeight, 2),
            () => items.PickManyWeightedDistinct(new DeterministicRandom(1), 2));
    }

    [Theory]
    [MemberData(nameof(InvalidTables))]
    public void ToWeightedSampler_ThrowsSameExceptionAsSelectorOverload(string table)
    {
        var items = TableFor(table);

        AssertSameFailure(
            () => WeightedSampler<LootEntry>.Create(items, WeightedTables.LootWeight),
            () => items.ToWeightedSampler());
    }

    /// <summary>
    /// A negative <see cref="double"/> weight through the single generated method a floating-point
    /// weight member gets.
    /// </summary>
    [Fact]
    public void PickWeighted_DoubleWeight_ThrowsSameExceptionAsSelectorOverload()
    {
        List<DoubleWeightedItem> items =
        [
            new() { Name = "a", Weight = 1.0 },
            new() { Name = "b", Weight = -0.5 },
        ];

        AssertSameFailure(
            () => new DeterministicRandom(1).PickWeighted(items, WeightedTables.DoubleWeight),
            () => items.PickWeighted(new DeterministicRandom(1)));
    }

    /// <summary>
    /// <c>count</c> is validated by the runtime overload as well, so it must come back through the
    /// generated method unchanged (including the offending value carried on the exception).
    /// </summary>
    [Fact]
    public void PickManyWeighted_NonPositiveCount_ThrowsSameExceptionAsSelectorOverload()
    {
        var items = WeightedTables.Loot();

        AssertSameFailure(
            () => new DeterministicRandom(1).PickManyWeighted(items, WeightedTables.LootWeight, 0),
            () => items.PickManyWeighted(new DeterministicRandom(1), 0));
    }

    /// <summary>
    /// Runs the selector-based call and the generated call, and asserts both failed identically.
    /// </summary>
    private static void AssertSameFailure(Func<object> selectorCall, Func<object> generatedCall)
    {
        var expected = Assert.ThrowsAny<ArgumentException>(selectorCall);
        var actual = Assert.ThrowsAny<ArgumentException>(generatedCall);

        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.ParamName, actual.ParamName);
        Assert.Equal(expected.Message, actual.Message);
    }
}
