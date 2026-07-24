using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SsalKit.Randomness;

/// <summary>
/// An <see cref="IRandomSource"/> backed by a cryptographically secure random number generator
/// (<see cref="RandomNumberGenerator"/>).
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="DeterministicRandom"/>, this source is unpredictable and safe for
/// security-sensitive purposes (tokens, secrets, shuffling anything that must stay confidential).
/// It is not seedable and cannot reproduce a sequence.
/// </para>
/// <para>
/// <b>Thread-safe.</b> <see cref="RandomNumberGenerator.Fill(Span{byte})"/> is a static,
/// thread-safe operation, so this type is exposed as a single shared <see cref="Instance"/>
/// rather than requiring per-use instantiation.
/// </para>
/// </remarks>
public sealed class CryptoRandomSource : IRandomSource
{
    /// <summary>
    /// Gets the shared, thread-safe <see cref="CryptoRandomSource"/> instance.
    /// </summary>
    public static CryptoRandomSource Instance { get; } = new();

    private CryptoRandomSource()
    {
    }

    /// <summary>
    /// Produces the next uniformly distributed, cryptographically secure 64-bit unsigned integer.
    /// </summary>
    /// <returns>A value uniformly distributed over the full range of <see cref="ulong"/>.</returns>
    public ulong NextUInt64()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with cryptographically secure random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill. May be empty.</param>
    public void NextBytes(Span<byte> buffer) => RandomNumberGenerator.Fill(buffer);
}
