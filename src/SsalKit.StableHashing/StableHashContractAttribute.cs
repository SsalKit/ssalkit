namespace SsalKit.StableHashing;

/// <summary>
/// Marks a type as a stable-hash contract. Together with <see cref="StableHashMemberAttribute"/>
/// on the type's members, this drives SsalKit.StableHashing.Generator (a separate, not-yet-shipped
/// package) to emit a <c>ComputeStableHash()</c> extension method for the type. Until that
/// generator exists, this attribute can still be used purely as documentation, or consumers can
/// hand-write encoding logic against <see cref="StableHashWriter"/> directly.
/// </summary>
/// <remarks>
/// <see cref="Name"/> and <see cref="Version"/> together are the permanent identity written into
/// every hash produced for this contract (see <see cref="StableHashWriter"/> remarks for the full
/// encoding contract). Renaming the .NET type itself never changes produced hashes; changing
/// <see cref="Name"/> or <see cref="Version"/> always does.
/// </remarks>
/// <param name="name">
/// The contract's stable name, independent of the CLR type name so the type can be freely renamed
/// without changing produced hashes. Encoded as part of the contract header (UTF-8, length-prefixed
/// — see <see cref="StableHashWriter.AppendContractHeader(string, int)"/>).
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class StableHashContractAttribute(string name) : Attribute
{
    /// <summary>
    /// The contract's stable name, encoded into every hash produced for this type. Must not be
    /// null or whitespace-only.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The contract version, encoded into every hash produced for this type. Increment this when
    /// the contract's member set or member types change in a way that should be reflected in the
    /// hash (i.e. that should invalidate previously stored checksums); leave at the default (1)
    /// otherwise. Must be 1 or greater.
    /// </summary>
    public int Version { get; set; } = 1;
}
