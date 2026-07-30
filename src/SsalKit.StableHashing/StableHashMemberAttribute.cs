namespace SsalKit.StableHashing;

/// <summary>
/// Marks a field or property as part of its declaring type's stable-hash contract, and assigns it
/// the stable member identifier that is encoded ahead of its value (see
/// <see cref="StableHashWriter.AppendMemberId(int)"/>). Members without this attribute are simply
/// excluded from the contract — opting a member in is always explicit, by design.
/// </summary>
/// <remarks>
/// Members are encoded in ascending <see cref="Id"/> order, not declaration order, so reordering
/// members in source does not change the hash. Renaming a member is likewise safe. Changing a
/// member's <see cref="Id"/>, or the type of the value it produces, changes the hash (see
/// <see cref="StableHashWriter"/> remarks for the full permanence contract).
/// </remarks>
/// <param name="id">
/// The member's stable identifier. Must be 1 or greater. Reusing the same value for two members of
/// the same contract makes the contract ambiguous and is expected to be rejected by the generator.
/// </param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class StableHashMemberAttribute(int id) : Attribute
{
    /// <summary>The member's stable identifier, encoded via <see cref="StableHashWriter.AppendMemberId(int)"/>.</summary>
    public int Id { get; } = id;
}
