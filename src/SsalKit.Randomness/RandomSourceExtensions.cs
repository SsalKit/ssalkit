namespace SsalKit.Randomness;

/// <summary>
/// Uniform-distribution extension methods for any <see cref="IRandomSource"/>: ranged integers,
/// floating-point values, booleans, shuffling, and single-item picks.
/// </summary>
/// <remarks>
/// Every member here has the same signature and semantics as the corresponding instance method on
/// <see cref="DeterministicRandom"/>, and both routes funnel through the same
/// <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/> helper, so a
/// <see cref="DeterministicRandom"/> used through its instance methods and the same instance used
/// through these extension methods (via its <see cref="IRandomSource"/> interface) always produce
/// identical sequences for identical starting state.
/// </remarks>
public static class RandomSourceExtensions
{
    /// <summary>
    /// Value-type adapter over an <see cref="IRandomSource"/>, implementing
    /// <see cref="IUInt64Generator"/> so <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/>
    /// can be called with this concrete struct type instead of a delegate.
    /// </summary>
    private readonly struct SourceGenerator(IRandomSource source) : IUInt64Generator
    {
        public ulong NextUInt64() => source.NextUInt64();
    }

    /// <summary>
    /// Returns a non-negative random integer in the range <c>[0, int.MaxValue)</c>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <returns>A value in <c>[0, int.MaxValue)</c>. <see cref="int.MaxValue"/> itself is never returned.</returns>
    public static int Next(this IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var generator = new SourceGenerator(source);
        return (int)RandomAlgorithms.NextUInt64Bounded(ref generator, int.MaxValue);
    }

    /// <summary>
    /// Returns a non-negative random integer in the range <c>[0, maxValue)</c>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <param name="maxValue">The exclusive upper bound. Must be non-negative.</param>
    /// <returns>A value in <c>[0, maxValue)</c>, or 0 if <paramref name="maxValue"/> is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is negative.</exception>
    public static int Next(this IRandomSource source, int maxValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maxValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "maxValue must be non-negative.");
        }

        if (maxValue == 0)
        {
            return 0;
        }

        var generator = new SourceGenerator(source);
        return (int)RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)maxValue);
    }

    /// <summary>
    /// Returns a random integer in the range <c>[minValue, maxValue)</c>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <param name="minValue">The inclusive lower bound.</param>
    /// <param name="maxValue">The exclusive upper bound.</param>
    /// <returns>
    /// A value in <c>[minValue, maxValue)</c>, or <paramref name="minValue"/> if the two bounds
    /// are equal.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    public static int Next(this IRandomSource source, int minValue, int maxValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "minValue must be less than or equal to maxValue.");
        }

        if (minValue == maxValue)
        {
            return minValue;
        }

        ulong range = (ulong)((long)maxValue - (long)minValue);
        var generator = new SourceGenerator(source);
        return minValue + (int)RandomAlgorithms.NextUInt64Bounded(ref generator, range);
    }

    /// <summary>
    /// Returns a non-negative random 64-bit integer in the range <c>[0, long.MaxValue)</c>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <returns>A value in <c>[0, long.MaxValue)</c>. <see cref="long.MaxValue"/> itself is never returned.</returns>
    public static long NextInt64(this IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var generator = new SourceGenerator(source);
        return (long)RandomAlgorithms.NextUInt64Bounded(ref generator, long.MaxValue);
    }

    /// <summary>
    /// Returns a non-negative random 64-bit integer in the range <c>[0, maxValue)</c>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <param name="maxValue">The exclusive upper bound. Must be non-negative.</param>
    /// <returns>A value in <c>[0, maxValue)</c>, or 0 if <paramref name="maxValue"/> is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is negative.</exception>
    public static long NextInt64(this IRandomSource source, long maxValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maxValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "maxValue must be non-negative.");
        }

        if (maxValue == 0)
        {
            return 0;
        }

        var generator = new SourceGenerator(source);
        return (long)RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)maxValue);
    }

    /// <summary>
    /// Returns a random 64-bit integer in the range <c>[minValue, maxValue)</c>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <param name="minValue">The inclusive lower bound.</param>
    /// <param name="maxValue">The exclusive upper bound.</param>
    /// <returns>
    /// A value in <c>[minValue, maxValue)</c>, or <paramref name="minValue"/> if the two bounds
    /// are equal.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    public static long NextInt64(this IRandomSource source, long minValue, long maxValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "minValue must be less than or equal to maxValue.");
        }

        if (minValue == maxValue)
        {
            return minValue;
        }

        // Two's-complement subtraction wraps correctly here even for the extreme case
        // minValue = long.MinValue, maxValue = long.MaxValue (an exclusive range of 2^64 - 1),
        // which would overflow if computed in signed arithmetic.
        ulong range = unchecked((ulong)maxValue - (ulong)minValue);
        var generator = new SourceGenerator(source);
        ulong offset = RandomAlgorithms.NextUInt64Bounded(ref generator, range);
        return unchecked(minValue + (long)offset);
    }

    /// <summary>
    /// Returns a random floating-point number in the range <c>[0, 1)</c>, with 53 bits of
    /// precision.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <returns>A value in <c>[0, 1)</c>. 1.0 is never returned.</returns>
    public static double NextDouble(this IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return (source.NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// Returns a random single-precision floating-point number in the range <c>[0, 1)</c>, with
    /// 24 bits of precision.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <returns>A value in <c>[0, 1)</c>. 1.0f is never returned.</returns>
    public static float NextSingle(this IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return (source.NextUInt64() >> 40) * (1.0f / (1UL << 24));
    }

    /// <summary>
    /// Returns a random boolean, derived from the most significant bit of a
    /// <see cref="IRandomSource.NextUInt64"/> draw.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <returns><see langword="true"/> or <see langword="false"/>, each with probability 0.5.</returns>
    public static bool NextBoolean(this IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return (source.NextUInt64() >> 63) != 0;
    }

    /// <summary>
    /// Shuffles <paramref name="values"/> in place using the Fisher–Yates algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="values">The span to shuffle in place.</param>
    public static void Shuffle<T>(this IRandomSource source, Span<T> values)
    {
        ArgumentNullException.ThrowIfNull(source);

        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = source.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    /// <summary>
    /// Shuffles <paramref name="values"/> in place using the Fisher–Yates algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="values">The list to shuffle in place.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public static void Shuffle<T>(this IRandomSource source, IList<T> values)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(values);

        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = source.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    /// <summary>
    /// Returns a single uniformly random element from <paramref name="items"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <returns>A uniformly randomly selected element of <paramref name="items"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
    public static T Pick<T>(this IRandomSource source, ReadOnlySpan<T> items)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (items.Length == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        return items[source.Next(items.Length)];
    }

    /// <summary>
    /// Returns a single uniformly random element from <paramref name="items"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be <see langword="null"/> or empty.</param>
    /// <returns>A uniformly randomly selected element of <paramref name="items"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
    public static T Pick<T>(this IRandomSource source, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        return items[source.Next(items.Count)];
    }
}
