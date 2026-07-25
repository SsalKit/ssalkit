using System.Buffers.Binary;

namespace SsalKit.Randomness;

/// <summary>
/// An <see cref="IRandomSource"/> adapter over an arbitrary <see cref="Random"/> instance, for
/// interop with existing code and for tests that need a reproducible, seedable
/// <see cref="Random"/>-backed source.
/// </summary>
/// <remarks>
/// <b>Thread safety</b> follows whatever <see cref="Random"/> instance is wrapped: a plain
/// <c>new Random(seed)</c> is not thread-safe, while <see cref="Random.Shared"/> is (though
/// <see cref="SharedRandomSource"/> should be preferred for that case).
/// </remarks>
/// <param name="random">The <see cref="Random"/> instance to wrap.</param>
public sealed class SystemRandomSource(Random random) : IRandomSource
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    /// <summary>
    /// Produces the next uniformly distributed 64-bit unsigned integer from the wrapped
    /// <see cref="Random"/> instance.
    /// </summary>
    /// <returns>A value uniformly distributed over the full range of <see cref="ulong"/>.</returns>
    /// <remarks>
    /// Implemented by filling an 8-byte buffer via <see cref="Random.NextBytes(Span{byte})"/> and
    /// reading it back in little-endian order, so the full 64 bits are uniformly distributed.
    /// </remarks>
    public ulong NextUInt64()
    {
        Span<byte> bytes = stackalloc byte[8];
        _random.NextBytes(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with random bytes from the wrapped <see cref="Random"/> instance.
    /// </summary>
    /// <param name="buffer">The buffer to fill. May be empty.</param>
    public void NextBytes(Span<byte> buffer) => _random.NextBytes(buffer);
}
