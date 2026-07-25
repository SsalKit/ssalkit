namespace SsalKit.Randomness.IntegrationTests.TestModels;

// Every type in this file is a real consumer of [RandomWeight]: the source generator runs over
// this project (referenced as an analyzer from the csproj) and emits a `<Type>RandomWeightExtensions`
// class next to each one. The tests then call those generated extensions directly -- nothing here
// is a stand-in or a hand-written copy of generated code.

/// <summary>
/// The canonical shape: public type, public <see cref="long"/> weight property. Yields the full
/// generated surface (<c>PickWeighted</c> / <c>PickManyWeighted</c> / <c>PickManyWeightedDistinct</c>
/// / <c>ToWeightedSampler</c>) on a <c>public</c> extension class.
/// </summary>
public sealed class LootEntry
{
    public required string ItemId { get; init; }

    [RandomWeight]
    public long Weight { get; init; }

    public override string ToString() => ItemId;
}

/// <summary>
/// An <see cref="int"/> weight, to confirm the narrower integral types delegate to the same
/// <c>Func&lt;T, long&gt;</c> runtime overloads through the generated cast.
/// </summary>
public sealed class IntWeightedItem
{
    public required string Name { get; init; }

    [RandomWeight]
    public int Weight { get; init; }
}

/// <summary>
/// The weight held in a field rather than a property (both are supported targets).
/// </summary>
public sealed class FieldWeightedItem(string name, long weight)
{
    public readonly string Name = name;

    [RandomWeight]
    public readonly long Weight = weight;
}

/// <summary>
/// A <see cref="double"/> weight: only <c>PickWeighted</c> is generated, mirroring the runtime
/// surface, which offers batched draws and alias-table sampling for <see cref="long"/> weights only.
/// </summary>
public sealed class DoubleWeightedItem
{
    public required string Name { get; init; }

    [RandomWeight]
    public double Weight { get; init; }
}

/// <summary>
/// A <see cref="float"/> weight, which the generator classifies alongside <see cref="double"/>.
/// </summary>
public sealed class FloatWeightedItem
{
    public required string Name { get; init; }

    [RandomWeight]
    public float Weight { get; init; }
}

/// <summary>
/// An <see langword="internal"/> type: the generated extension class is capped at the declaring
/// type's effective accessibility and therefore comes out <see langword="internal"/> too.
/// </summary>
internal sealed class InternalWeightedItem
{
    public required string Name { get; init; }

    [RandomWeight]
    public long Weight { get; init; }
}

/// <summary>
/// A <see langword="public"/> type that opts out of a public extension class with
/// <c>[RandomWeight(InternalExtensions = true)]</c>.
/// </summary>
public sealed class ForcedInternalItem
{
    public required string Name { get; init; }

    [RandomWeight(InternalExtensions = true)]
    public long Weight { get; init; }
}

/// <summary>
/// Container for the nested-type case: the generated class flattens the containing type's name into
/// <c>WeightedContainer_NestedItemRandomWeightExtensions</c>, still top-level in this namespace.
/// </summary>
public static class WeightedContainer
{
    public sealed class NestedItem
    {
        public required string Name { get; init; }

        [RandomWeight]
        public long Weight { get; init; }
    }
}

/// <summary>
/// The base of the inheritance pair. Per the design's non-goals, the attribute here generates
/// extensions for <see cref="BaseLootEntry"/> only -- <see cref="DerivedLootEntry"/> gets none of
/// its own and reuses these through <c>IReadOnlyList&lt;out T&gt;</c> covariance.
/// </summary>
public class BaseLootEntry
{
    public required string ItemId { get; init; }

    [RandomWeight]
    public long Weight { get; init; }

    public override string ToString() => ItemId;
}

/// <summary>
/// The derived half of the inheritance pair. Carries no <c>[RandomWeight]</c> of its own.
/// </summary>
public sealed class DerivedLootEntry : BaseLootEntry
{
    public required string Rarity { get; init; }
}
