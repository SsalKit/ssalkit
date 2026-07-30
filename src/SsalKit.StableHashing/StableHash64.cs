using System.Globalization;

namespace SsalKit.StableHashing;

/// <summary>
/// A 64-bit checksum produced by <see cref="StableHashWriter"/>-based encoding: a value guaranteed
/// to be the same across processes, machines, and CPU architectures, for as long as the encoding
/// contract that produced it (contract name, version, and member set — see
/// <see cref="StableHashContractAttribute"/> and <see cref="StableHashWriter"/> remarks) does not
/// change.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a cryptographic hash.</b> There is no collision-resistance guarantee and no protection
/// against an adversary who wants to construct two different inputs that hash to the same value.
/// Do not use <see cref="StableHash64"/> for integrity checks against tampering, message
/// authentication, password storage, or any other purpose that needs cryptographic strength — use
/// <c>System.Security.Cryptography.SHA256</c> or similar for that.
/// </para>
/// <para>
/// Unlike <see cref="object.GetHashCode"/> — which is explicitly permitted to change between
/// processes and must never be persisted — a <see cref="StableHash64"/> value is safe to store in
/// a database, send over a network, or otherwise treat as a durable identifier, provided it was
/// produced by an encoding contract that has not changed since. See <see cref="StableHashWriter"/>
/// for the full permanence contract.
/// </para>
/// <para>
/// <see cref="Value"/> can be handed to <c>new SsalKit.Randomness.DeterministicRandom(hash.Value)</c>
/// (from the separate, optional SsalKit.Randomness package) to derive a named, reproducible random
/// stream from any hashable value. The two packages have no dependency on each other; this is a
/// documented usage pattern only.
/// </para>
/// </remarks>
/// <param name="Value">The raw 64-bit hash value.</param>
public readonly record struct StableHash64(ulong Value)
{
    /// <summary>
    /// Returns the hash as a lowercase, zero-padded, 16-character hexadecimal string (for example
    /// <c>"9c3f38517dbc66aa"</c>). This formatting is part of the type's permanent contract.
    /// </summary>
    /// <returns>The lowercase hexadecimal representation of <see cref="Value"/>.</returns>
    public override string ToString() => Value.ToString("x16", CultureInfo.InvariantCulture);
}
