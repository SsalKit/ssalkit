namespace SsalKit.Randomness;

/// <summary>
/// Weighted-random-pick extension methods for any <see cref="IRandomSource"/>: single-shot
/// weighted selection (<see cref="long"/> and <see cref="double"/> weights), batched selection
/// with replacement, and batched selection without replacement — plus
/// <see cref="ToWeightedSampler{T}"/>, a type-inferring way to build a
/// <see cref="WeightedSampler{T}"/> from a weighted item list.
/// </summary>
/// <remarks>
/// <para>
/// Every member here builds a cumulative-sum array in <c>O(n)</c> and then locates the drawn
/// position with a binary search in <c>O(log n)</c>. For repeated draws against the same
/// <c>long</c>-weighted item set, prefer <see cref="WeightedSampler{T}"/>, which builds an alias
/// table once and draws in <c>O(1)</c> thereafter.
/// </para>
/// <para>
/// See the exception contract table on each member below. It is applied uniformly across every
/// weighted-pick API in this type and in <see cref="WeightedSampler{T}"/>.
/// </para>
/// </remarks>
public static class WeightedRandomExtensions
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
    /// The largest element count for which a cumulative-sum array is stack-allocated in the
    /// zero-allocation span-based overloads. Larger inputs fall back to a heap array so an
    /// arbitrarily large caller-supplied span cannot overflow the stack.
    /// </summary>
    private const int MaxStackAllocElements = 256;

    // ---------------------------------------------------------------------
    // Single-shot picks
    // ---------------------------------------------------------------------

    /// <summary>
    /// Returns a single random element from <paramref name="items"/>, selected with probability
    /// proportional to the <see cref="long"/> weight <paramref name="weight"/> assigns to it.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weight">
    /// Computes the weight of an item. Must be non-negative for every item, and the sum of all
    /// weights must be positive. An item with weight 0 is never selected.
    /// </param>
    /// <returns>A weighted-randomly selected element of <paramref name="items"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/>, <paramref name="items"/>, or <paramref name="weight"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; a weight is negative; or the total weight is 0.
    /// </exception>
    /// <exception cref="OverflowException">The sum of all weights overflows <see cref="long"/>.</exception>
    public static T PickWeighted<T>(this IRandomSource source, IReadOnlyList<T> items, Func<T, long> weight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);

        if (items.Count == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        long[] cumulative = BuildLongCumulative(items, weight);
        return items[PickIndexFromLongCumulative(source, cumulative)];
    }

    /// <summary>
    /// Returns a single random element from <paramref name="items"/>, selected with probability
    /// proportional to the <see cref="double"/> weight <paramref name="weight"/> assigns to it.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weight">
    /// Computes the weight of an item. Must be non-negative and finite (not <c>NaN</c> or
    /// infinite) for every item, and the sum of all weights must be positive. An item with weight
    /// 0 is never selected.
    /// </param>
    /// <returns>A weighted-randomly selected element of <paramref name="items"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/>, <paramref name="items"/>, or <paramref name="weight"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; a weight is negative, <c>NaN</c>, or infinite; or the
    /// total weight is 0.
    /// </exception>
    public static T PickWeighted<T>(this IRandomSource source, IReadOnlyList<T> items, Func<T, double> weight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);

        if (items.Count == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        double[] cumulative = BuildDoubleCumulative(items, weight);
        return items[PickIndexFromDoubleCumulative(source, cumulative)];
    }

    /// <summary>
    /// Returns a single random element from <paramref name="items"/>, selected with probability
    /// proportional to the corresponding entry of <paramref name="weights"/>. Allocation-free.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weights">
    /// The weight of each item, parallel to <paramref name="items"/>. Every entry must be
    /// non-negative, and the sum must be positive. An entry of 0 is never selected.
    /// </param>
    /// <returns>A weighted-randomly selected element of <paramref name="items"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; <paramref name="items"/> and <paramref name="weights"/>
    /// have different lengths; a weight is negative; or the total weight is 0.
    /// </exception>
    /// <exception cref="OverflowException">The sum of all weights overflows <see cref="long"/>.</exception>
    public static T PickWeighted<T>(this IRandomSource source, ReadOnlySpan<T> items, ReadOnlySpan<long> weights)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (items.Length != weights.Length)
        {
            throw new ArgumentException("items and weights must have the same length.", nameof(weights));
        }

        if (items.Length == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        Span<long> cumulative = weights.Length <= MaxStackAllocElements
            ? stackalloc long[weights.Length]
            : new long[weights.Length];

        long sum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            long w = weights[i];
            if (w < 0)
            {
                throw new ArgumentException($"Weight at index {i} is negative.", nameof(weights));
            }

            checked
            {
                sum += w;
            }

            cumulative[i] = sum;
        }

        if (sum == 0)
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weights));
        }

        var generator = new SourceGenerator(source);
        ulong position = RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)sum);
        int index = BinarySearchCumulativeLong(cumulative, (long)position);
        return items[index];
    }

    /// <summary>
    /// Returns a single random element from <paramref name="items"/>, selected with probability
    /// proportional to the corresponding entry of <paramref name="weights"/>. Allocation-free.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weights">
    /// The weight of each item, parallel to <paramref name="items"/>. Every entry must be
    /// non-negative and finite (not <c>NaN</c> or infinite), and the sum must be positive. An
    /// entry of 0 is never selected.
    /// </param>
    /// <returns>A weighted-randomly selected element of <paramref name="items"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; <paramref name="items"/> and <paramref name="weights"/>
    /// have different lengths; a weight is negative, <c>NaN</c>, or infinite; or the total weight
    /// is 0.
    /// </exception>
    public static T PickWeighted<T>(this IRandomSource source, ReadOnlySpan<T> items, ReadOnlySpan<double> weights)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (items.Length != weights.Length)
        {
            throw new ArgumentException("items and weights must have the same length.", nameof(weights));
        }

        if (items.Length == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        Span<double> cumulative = weights.Length <= MaxStackAllocElements
            ? stackalloc double[weights.Length]
            : new double[weights.Length];

        double sum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            double w = weights[i];
            ValidateDoubleWeight(w, i, nameof(weights));

            sum += w;
            cumulative[i] = sum;
        }

        if (!(sum > 0))
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weights));
        }

        double position = sum * source.NextDouble();
        int index = BinarySearchCumulativeDouble(cumulative, position);
        return items[index];
    }

    // ---------------------------------------------------------------------
    // Batched picks
    // ---------------------------------------------------------------------

    /// <summary>
    /// Draws <paramref name="count"/> elements from <paramref name="items"/> with replacement
    /// (the same item may be drawn more than once), each draw independently weighted by
    /// <paramref name="weight"/>.
    /// </summary>
    /// <remarks>
    /// The cumulative-sum array is built once and reused for all <paramref name="count"/> draws,
    /// so this is equivalent to — but faster than — calling
    /// <see cref="PickWeighted{T}(IRandomSource, IReadOnlyList{T}, Func{T, long})"/> in a loop. It
    /// draws from <paramref name="source"/> in exactly the same order that loop would, so the two
    /// approaches produce identical sequences for identical starting state.
    /// </remarks>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weight">
    /// Computes the weight of an item. Must be non-negative for every item, and the sum of all
    /// weights must be positive.
    /// </param>
    /// <param name="count">The number of elements to draw. Must be greater than 0.</param>
    /// <returns>An array of <paramref name="count"/> weighted-randomly selected elements.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/>, <paramref name="items"/>, or <paramref name="weight"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; a weight is negative; or the total weight is 0.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not greater than 0.</exception>
    /// <exception cref="OverflowException">The sum of all weights overflows <see cref="long"/>.</exception>
    public static T[] PickManyWeighted<T>(this IRandomSource source, IReadOnlyList<T> items, Func<T, long> weight, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);

        if (items.Count == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be greater than zero.");
        }

        long[] cumulative = BuildLongCumulative(items, weight);
        var result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = items[PickIndexFromLongCumulative(source, cumulative)];
        }

        return result;
    }

    /// <summary>
    /// Draws <paramref name="count"/> distinct elements from <paramref name="items"/> without
    /// replacement (no item is drawn more than once), each draw weighted by <paramref name="weight"/>
    /// among the items not yet drawn.
    /// </summary>
    /// <remarks>
    /// Implemented as the "subtract the chosen item's weight from a running total and exclude it
    /// from future draws" linear strategy: each of the <paramref name="count"/> draws rebuilds a
    /// cumulative-sum array over the remaining weights, giving <c>O(count * n)</c> overall — this
    /// is adequate for the sizes this library targets and keeps the implementation simple; a
    /// Fenwick-tree-backed <c>O(count * log n)</c> version could replace this internally without
    /// an API change if a future workload needs it.
    /// </remarks>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The random source.</param>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weight">
    /// Computes the weight of an item. Must be non-negative for every item, and the sum of all
    /// weights must be positive. An item with weight 0 is never selected.
    /// </param>
    /// <param name="count">
    /// The number of distinct elements to draw. Must be greater than 0 and must not exceed the
    /// number of items with strictly positive weight.
    /// </param>
    /// <returns>
    /// An array of <paramref name="count"/> distinct, weighted-randomly selected elements, in
    /// selection order (not sorted).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/>, <paramref name="items"/>, or <paramref name="weight"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; a weight is negative; or the total weight is 0.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is not greater than 0, or exceeds the number of items with
    /// strictly positive weight.
    /// </exception>
    /// <exception cref="OverflowException">The sum of all weights overflows <see cref="long"/>.</exception>
    public static T[] PickManyWeightedDistinct<T>(this IRandomSource source, IReadOnlyList<T> items, Func<T, long> weight, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);

        if (items.Count == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be greater than zero.");
        }

        int n = items.Count;
        var remainingWeights = new long[n];
        long total = 0;
        int positiveCount = 0;
        for (int i = 0; i < n; i++)
        {
            long w = weight(items[i]);
            if (w < 0)
            {
                throw new ArgumentException($"Weight at index {i} is negative.", nameof(weight));
            }

            checked
            {
                total += w;
            }

            remainingWeights[i] = w;
            if (w > 0)
            {
                positiveCount++;
            }
        }

        if (total == 0)
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weight));
        }

        if (count > positiveCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must not exceed the number of items with strictly positive weight.");
        }

        var cumulative = new long[n];
        var result = new T[count];
        var generator = new SourceGenerator(source);
        long remainingTotal = total;
        for (int pick = 0; pick < count; pick++)
        {
            long sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += remainingWeights[i];
                cumulative[i] = sum;
            }

            ulong position = RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)remainingTotal);
            int index = BinarySearchCumulativeLong(cumulative, (long)position);

            result[pick] = items[index];
            remainingTotal -= remainingWeights[index];
            remainingWeights[index] = 0;
        }

        return result;
    }

    // ---------------------------------------------------------------------
    // Sampler construction
    // ---------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="WeightedSampler{T}"/> over <paramref name="items"/>, weighted by
    /// <paramref name="weight"/>, for repeated <c>O(1)</c> draws from a fixed item set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thin, inference-friendly wrapper over
    /// <see cref="WeightedSampler{T}.Create(IReadOnlyList{T}, Func{T, long})"/>: as a static
    /// factory on a generic class, <c>Create</c> requires the element type to be written out
    /// (<c>WeightedSampler&lt;LootEntry&gt;.Create(...)</c>), while this extension infers it from
    /// the receiver. Validation, the alias-table construction, and the entire exception contract
    /// live in <c>Create</c> — this method only forwards.
    /// </para>
    /// <para>
    /// <b>Build once, draw many.</b> Building the alias table is <c>O(n)</c>; only the draws are
    /// <c>O(1)</c>. Calling this inside a draw loop rebuilds the table on every iteration and
    /// negates the reason to use a sampler at all — build one sampler per weighted table, hold on
    /// to it (it is immutable and thread-safe), and call <see cref="WeightedSampler{T}.Pick"/> on
    /// it repeatedly. For a single draw, use
    /// <see cref="PickWeighted{T}(IRandomSource, IReadOnlyList{T}, Func{T, long})"/> instead.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weight">
    /// Computes the weight of an item. Must be non-negative for every item, and the sum of all
    /// weights must be positive. An item with weight 0 is never selected.
    /// </param>
    /// <returns>A new, immutable <see cref="WeightedSampler{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="items"/> or <paramref name="weight"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; a weight is negative; or the total weight is 0.
    /// </exception>
    /// <exception cref="OverflowException">The sum of all weights overflows <see cref="long"/>.</exception>
    public static WeightedSampler<T> ToWeightedSampler<T>(this IReadOnlyList<T> items, Func<T, long> weight)
        => WeightedSampler<T>.Create(items, weight);

    // ---------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Validates a single <see cref="double"/> weight against the exception contract shared by
    /// every double-weighted API (non-negative, not <c>NaN</c>, not infinite).
    /// </summary>
    private static void ValidateDoubleWeight(double weight, int index, string paramName)
    {
        if (double.IsNaN(weight) || double.IsInfinity(weight))
        {
            throw new ArgumentException($"Weight at index {index} is NaN or Infinity.", paramName);
        }

        if (weight < 0)
        {
            throw new ArgumentException($"Weight at index {index} is negative.", paramName);
        }
    }

    /// <summary>
    /// Builds a <see cref="long"/> cumulative-sum array over <paramref name="items"/>, validating
    /// each weight and the total per the shared exception contract.
    /// </summary>
    private static long[] BuildLongCumulative<T>(IReadOnlyList<T> items, Func<T, long> weight)
    {
        int n = items.Count;
        var cumulative = new long[n];
        long sum = 0;
        for (int i = 0; i < n; i++)
        {
            long w = weight(items[i]);
            if (w < 0)
            {
                throw new ArgumentException($"Weight at index {i} is negative.", nameof(weight));
            }

            checked
            {
                sum += w;
            }

            cumulative[i] = sum;
        }

        if (sum == 0)
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weight));
        }

        return cumulative;
    }

    /// <summary>
    /// Builds a <see cref="double"/> cumulative-sum array over <paramref name="items"/>,
    /// validating each weight and the total per the shared exception contract.
    /// </summary>
    private static double[] BuildDoubleCumulative<T>(IReadOnlyList<T> items, Func<T, double> weight)
    {
        int n = items.Count;
        var cumulative = new double[n];
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double w = weight(items[i]);
            ValidateDoubleWeight(w, i, nameof(weight));

            sum += w;
            cumulative[i] = sum;
        }

        if (!(sum > 0))
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weight));
        }

        return cumulative;
    }

    /// <summary>
    /// Draws a bounded position against the total of <paramref name="cumulative"/> and returns
    /// the index of the containing bucket.
    /// </summary>
    private static int PickIndexFromLongCumulative(IRandomSource source, ReadOnlySpan<long> cumulative)
    {
        long total = cumulative[^1];
        var generator = new SourceGenerator(source);
        ulong position = RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)total);
        return BinarySearchCumulativeLong(cumulative, (long)position);
    }

    /// <summary>
    /// Draws a bounded position against the total of <paramref name="cumulative"/> and returns
    /// the index of the containing bucket.
    /// </summary>
    private static int PickIndexFromDoubleCumulative(IRandomSource source, ReadOnlySpan<double> cumulative)
    {
        double total = cumulative[^1];
        double position = total * source.NextDouble();
        return BinarySearchCumulativeDouble(cumulative, position);
    }

    /// <summary>
    /// Finds the smallest index whose cumulative sum strictly exceeds <paramref name="position"/>
    /// (i.e. an upper-bound binary search). An item's bucket is
    /// <c>[cumulative[i - 1], cumulative[i])</c> (with an implicit lower bound of 0 for index 0),
    /// so a zero-weight item — whose cumulative sum equals its predecessor's — has an empty
    /// bucket and can never be the result.
    /// </summary>
    private static int BinarySearchCumulativeLong(ReadOnlySpan<long> cumulative, long position)
    {
        int lo = 0;
        int hi = cumulative.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (cumulative[mid] <= position)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>
    /// Finds the smallest index whose cumulative sum strictly exceeds <paramref name="position"/>
    /// (i.e. an upper-bound binary search). The search is bounded to <c>[0, Length - 1]</c>
    /// regardless of <paramref name="position"/>'s magnitude, so the rare floating-point rounding
    /// case where <c>total * NextDouble()</c> rounds up to (but is mathematically always strictly
    /// less than) the true total simply resolves to the last index rather than going out of
    /// range.
    /// </summary>
    private static int BinarySearchCumulativeDouble(ReadOnlySpan<double> cumulative, double position)
    {
        int lo = 0;
        int hi = cumulative.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (cumulative[mid] <= position)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
