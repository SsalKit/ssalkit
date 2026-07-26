namespace SsalKit.Randomness;

/// <summary>
/// The full 256-bit state of a <see cref="DeterministicRandom"/> instance (the four
/// <see cref="ulong"/> words of the xoshiro256** state array). Value-equatable and trivially
/// serializable (including with <c>System.Text.Json</c>), so it can be persisted or transmitted
/// and later used with <see cref="DeterministicRandom.FromState(RandomState)"/> to resume an
/// identical output sequence.
/// </summary>
/// <remarks>
/// <c>System.Text.Json</c> round-trips this type losslessly out of the box: the four words are
/// written as JSON numbers and read back as exact <see cref="ulong"/> values. Note that JSON
/// numbers are only guaranteed to survive a JavaScript consumer up to 2^53 — a state word above
/// that (the common case, since state words are uniformly distributed over the whole
/// <see cref="ulong"/> range) loses precision if it is parsed into a JavaScript <c>number</c>.
/// Serialize the words as strings (or use <see cref="ToArray"/> with a binary format) when the
/// state has to cross a JavaScript boundary.
/// </remarks>
/// <param name="S0">The first state word.</param>
/// <param name="S1">The second state word.</param>
/// <param name="S2">The third state word.</param>
/// <param name="S3">The fourth state word.</param>
public readonly record struct RandomState(ulong S0, ulong S1, ulong S2, ulong S3)
{
    /// <summary>
    /// Gets a value indicating whether this state is usable by xoshiro256**. The all-zero state
    /// is invalid: xoshiro256** never leaves the all-zero state once it enters it, so every
    /// subsequent output would be zero.
    /// </summary>
    public bool IsValid => (S0 | S1 | S2 | S3) != 0;

    /// <summary>
    /// Copies this state into a new four-element array, in <c>[S0, S1, S2, S3]</c> order. Provided
    /// for interoperability with storage layouts that use a plain <c>ulong[4]</c>.
    /// </summary>
    /// <returns>A new four-element array containing the state words.</returns>
    public ulong[] ToArray() => [S0, S1, S2, S3];

    /// <summary>
    /// Copies this state into <paramref name="destination"/>, in <c>[S0, S1, S2, S3]</c> order,
    /// without allocating.
    /// </summary>
    /// <param name="destination">The destination span. Must have a length of at least 4.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> has a length less than 4.
    /// </exception>
    public void CopyTo(Span<ulong> destination)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Destination span must have a length of at least 4.", nameof(destination));
        }

        destination[0] = S0;
        destination[1] = S1;
        destination[2] = S2;
        destination[3] = S3;
    }

    /// <summary>
    /// Creates a <see cref="RandomState"/> from the first four elements of <paramref name="source"/>,
    /// in <c>[S0, S1, S2, S3]</c> order.
    /// </summary>
    /// <param name="source">The source span. Must have a length of at least 4.</param>
    /// <returns>The resulting state.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> has a length less than 4, or the resulting state is the
    /// all-zero state (see <see cref="IsValid"/>).
    /// </exception>
    public static RandomState FromSpan(ReadOnlySpan<ulong> source)
    {
        if (source.Length < 4)
        {
            throw new ArgumentException("Source span must have a length of at least 4.", nameof(source));
        }

        var state = new RandomState(source[0], source[1], source[2], source[3]);
        if (!state.IsValid)
        {
            throw new ArgumentException("The all-zero state is not a valid xoshiro256** state.", nameof(source));
        }

        return state;
    }
}
