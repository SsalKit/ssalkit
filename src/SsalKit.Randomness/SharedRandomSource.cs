using System.Buffers.Binary;

namespace SsalKit.Randomness;

/// <summary>
/// An <see cref="IRandomSource"/> backed by <see cref="Random.Shared"/>.
/// </summary>
/// <remarks>
/// <b>Thread-safe.</b> <see cref="Random.Shared"/> itself is thread-safe, so this type is exposed
/// as a single shared <see cref="Instance"/> rather than requiring per-use instantiation. This
/// source is not seedable and cannot reproduce a sequence.
/// </remarks>
public sealed class SharedRandomSource : IRandomSource
{
    /// <summary>
    /// Gets the shared, thread-safe <see cref="SharedRandomSource"/> instance.
    /// </summary>
    public static SharedRandomSource Instance { get; } = new();

    private SharedRandomSource()
    {
    }

    /// <summary>
    /// Produces the next uniformly distributed 64-bit unsigned integer from <see cref="Random.Shared"/>.
    /// </summary>
    /// <returns>A value uniformly distributed over the full range of <see cref="ulong"/>.</returns>
    /// <remarks>
    /// Implemented by filling an 8-byte buffer via <see cref="Random.NextBytes(Span{byte})"/> and
    /// reading it back in little-endian order, so the full 64 bits are uniformly distributed
    /// (rather than composing two 32-bit or two <see cref="Random.NextInt64()"/> draws, which do
    /// not cover the full <see cref="ulong"/> range uniformly).
    /// </remarks>
    public ulong NextUInt64()
    {
        Span<byte> bytes = stackalloc byte[8];
        Random.Shared.NextBytes(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with random bytes from <see cref="Random.Shared"/>.
    /// </summary>
    /// <param name="buffer">The buffer to fill. May be empty.</param>
    public void NextBytes(Span<byte> buffer) => Random.Shared.NextBytes(buffer);
}
