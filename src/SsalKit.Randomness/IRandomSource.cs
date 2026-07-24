namespace SsalKit.Randomness;

/// <summary>
/// The minimal contract shared by every random source in SsalKit.Randomness: a uniformly
/// distributed 64-bit generator plus a buffer-filling primitive. All higher-level operations
/// (ranged integers, doubles, booleans, shuffling, weighted picks) are derived from these two
/// members via extension methods, so every implementation automatically gains the same
/// algorithm and the same correctness guarantees.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Produces the next uniformly distributed 64-bit unsigned integer from this source.
    /// </summary>
    /// <returns>A value uniformly distributed over the full range of <see cref="ulong"/>.</returns>
    ulong NextUInt64();

    /// <summary>
    /// Fills <paramref name="buffer"/> with random bytes.
    /// </summary>
    /// <param name="buffer">The buffer to fill. May be empty.</param>
    void NextBytes(Span<byte> buffer);
}
