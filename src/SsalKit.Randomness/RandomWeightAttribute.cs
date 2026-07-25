namespace SsalKit.Randomness;

/// <summary>
/// Marks the property or field that holds an item's weight, so the SsalKit.Randomness source
/// generator can emit selector-less weighted-picking extension methods for the declaring type.
/// </summary>
/// <remarks>
/// <para>
/// The generator emits one static extension class per decorated type, containing collection-receiver
/// overloads that take the <see cref="IRandomSource"/> explicitly and delegate straight to the
/// selector-based runtime APIs on <see cref="WeightedRandomExtensions"/> and
/// <see cref="WeightedSampler{T}"/>. In other words, decorating <c>LootEntry.Weight</c> turns
/// <c>random.PickWeighted(lootTable, static x =&gt; x.Weight)</c> into
/// <c>lootTable.PickWeighted(random)</c> — the same call, with the selector written for you at
/// compile time. There is no reflection and no runtime dispatch: the generated code is ordinary
/// C# that is AOT- and trimming-safe.
/// </para>
/// <para>
/// Which extensions are generated depends on the weight member's type. An integral member
/// (<see cref="sbyte"/>, <see cref="byte"/>, <see cref="short"/>, <see cref="ushort"/>,
/// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>) yields the full set —
/// <c>PickWeighted</c>, <c>PickManyWeighted</c>, <c>PickManyWeightedDistinct</c>, and
/// <c>ToWeightedSampler</c>. A floating-point member (<see cref="float"/>, <see cref="double"/>)
/// yields only <c>PickWeighted</c>, mirroring the runtime surface, which offers batched draws and
/// alias-table sampling for <see cref="long"/> weights only. Any other member type is reported as
/// a compile-time diagnostic rather than silently ignored.
/// </para>
/// <para>
/// The generated extension class is <see langword="public"/> by default, capped at the effective
/// accessibility of the decorated type (an <see langword="internal"/> type therefore yields
/// <see langword="internal"/> extensions automatically). Set <see cref="InternalExtensions"/> to
/// <see langword="true"/> to force <see langword="internal"/> extensions even for a
/// <see langword="public"/> type.
/// </para>
/// <para>
/// This attribute has no runtime behaviour of its own. It carries no state that anything reads at
/// run time, and if the source generator is not running (for example, when only the reference
/// assembly is consumed, or an older compiler host declines to load analyzers), the attribute is
/// simply inert: nothing is generated, nothing fails, and no existing behaviour changes.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class LootEntry
/// {
///     public required string ItemId { get; init; }
///
///     [RandomWeight]
///     public long Weight { get; init; }
/// }
///
/// // Generated, in the same namespace as LootEntry:
/// LootEntry drop = lootTable.PickWeighted(random);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class RandomWeightAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether the generated extension class should be declared
    /// <see langword="internal"/> even when the decorated type is <see langword="public"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>, which generates a <see langword="public"/> extension
    /// class so a shared model assembly can expose the picking helpers to its consumers. Set it to
    /// <see langword="true"/> to keep the generated helpers out of the declaring assembly's public
    /// API surface. It has no effect when the decorated type's effective accessibility is already
    /// <see langword="internal"/> or narrower, since the generated class is capped at that
    /// accessibility regardless.
    /// </remarks>
    public bool InternalExtensions { get; set; }
}
