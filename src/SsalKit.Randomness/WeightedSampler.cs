namespace SsalKit.Randomness;

/// <summary>
/// A pre-built, immutable sampler for repeated weighted draws from a fixed set of
/// <see cref="long"/>-weighted items.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread-safe.</b> Unlike <see cref="DeterministicRandom"/>, a <see cref="WeightedSampler{T}"/>
/// instance is fully immutable once returned by <see cref="Create(IReadOnlyList{T}, Func{T, long})"/>
/// or <see cref="Create(ReadOnlySpan{T}, ReadOnlySpan{long})"/>: all validation and table
/// construction happens during <c>Create</c>, and every draw-time method
/// (<see cref="Pick(IRandomSource)"/>, <see cref="PickMany(IRandomSource, int)"/>) reads only that
/// immutable table plus whatever <see cref="IRandomSource"/> the caller supplies. Multiple threads
/// may therefore call <see cref="Pick(IRandomSource)"/> concurrently on the same
/// <see cref="WeightedSampler{T}"/> instance, each passing its own <see cref="IRandomSource"/> —
/// exactly mirroring why <see cref="DeterministicRandom"/> itself is documented as not
/// thread-safe: the state that would need synchronizing lives entirely in the caller-supplied
/// source, not in this type.
/// </para>
/// <para>
/// Building the table is <c>O(n)</c> (Walker/Vose alias method, using exact integer arithmetic —
/// no floating-point probability table); each <see cref="Pick(IRandomSource)"/> call is
/// <c>O(1)</c>, drawing exactly two bounded values from the supplied source (one to choose a
/// column, one to choose between that column's item and its alias). Prefer this type over the
/// single-shot <see cref="WeightedRandomExtensions.PickWeighted{T}(IRandomSource, IReadOnlyList{T}, Func{T, long})"/>
/// family when drawing repeatedly from the same weighted item set.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public sealed class WeightedSampler<T>
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

    private readonly T[] _items;

    // _threshold[i] is an exact integer numerator over the denominator _total: drawing a uniform
    // position in [0, _total) and comparing it against _threshold[i] decides, with exact
    // probability _threshold[i] / _total, whether column i keeps itself or defers to _alias[i].
    private readonly long[] _threshold;
    private readonly int[] _alias;
    private readonly long _total;

    private WeightedSampler(T[] items, long[] threshold, int[] alias, long total)
    {
        _items = items;
        _threshold = threshold;
        _alias = alias;
        _total = total;
    }

    /// <summary>
    /// Gets the number of distinct items this sampler was built from.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>
    /// Builds a sampler over <paramref name="items"/>, weighted by <paramref name="weight"/>.
    /// </summary>
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
    public static WeightedSampler<T> Create(IReadOnlyList<T> items, Func<T, long> weight)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);

        int n = items.Count;
        if (n == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        var itemsArray = new T[n];
        var weights = new long[n];
        long total = 0;
        for (int i = 0; i < n; i++)
        {
            T item = items[i];
            long w = weight(item);
            if (w < 0)
            {
                throw new ArgumentException($"Weight at index {i} is negative.", nameof(weight));
            }

            checked
            {
                total += w;
            }

            itemsArray[i] = item;
            weights[i] = w;
        }

        if (total == 0)
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weight));
        }

        return BuildAliasTable(itemsArray, weights, total);
    }

    /// <summary>
    /// Builds a sampler over <paramref name="items"/>, weighted by the corresponding entry of
    /// <paramref name="weights"/>.
    /// </summary>
    /// <param name="items">The candidate items. Must not be empty.</param>
    /// <param name="weights">
    /// The weight of each item, parallel to <paramref name="items"/>. Every entry must be
    /// non-negative, and the sum must be positive. An entry of 0 is never selected.
    /// </param>
    /// <returns>A new, immutable <see cref="WeightedSampler{T}"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="items"/> is empty; <paramref name="items"/> and <paramref name="weights"/>
    /// have different lengths; a weight is negative; or the total weight is 0.
    /// </exception>
    /// <exception cref="OverflowException">The sum of all weights overflows <see cref="long"/>.</exception>
    public static WeightedSampler<T> Create(ReadOnlySpan<T> items, ReadOnlySpan<long> weights)
    {
        if (items.Length != weights.Length)
        {
            throw new ArgumentException("items and weights must have the same length.", nameof(weights));
        }

        int n = items.Length;
        if (n == 0)
        {
            throw new ArgumentException("items must not be empty.", nameof(items));
        }

        var itemsArray = new T[n];
        var weightsArray = new long[n];
        long total = 0;
        for (int i = 0; i < n; i++)
        {
            long w = weights[i];
            if (w < 0)
            {
                throw new ArgumentException($"Weight at index {i} is negative.", nameof(weights));
            }

            checked
            {
                total += w;
            }

            itemsArray[i] = items[i];
            weightsArray[i] = w;
        }

        if (total == 0)
        {
            throw new ArgumentException("The total weight must be greater than zero.", nameof(weights));
        }

        return BuildAliasTable(itemsArray, weightsArray, total);
    }

    /// <summary>
    /// Builds the Walker/Vose alias table for <paramref name="items"/>/<paramref name="weights"/>
    /// using exact integer arithmetic throughout (no floating-point probability table).
    /// </summary>
    /// <remarks>
    /// Each weight is scaled by the item count <c>n</c> (<c>scaled[i] = n * weights[i]</c>,
    /// computed in <see cref="Int128"/> so the multiplication cannot silently overflow even when
    /// <c>weights[i]</c> is close to <see cref="long.MaxValue"/>) and compared against the total
    /// <paramref name="total"/> to classify each item as "small" (<c>scaled[i] &lt; total</c>) or
    /// "large" (<c>scaled[i] &gt;= total</c>). Small/large pairs are repeatedly matched: the small
    /// item's exact numerator becomes its threshold (it is <c>threshold[i] / total</c> likely to
    /// keep itself when its column is drawn) and its alias is set to the paired large item, whose
    /// scaled weight is then reduced by exactly what the small item did not need
    /// (<c>scaled[g] += scaled[l] - total</c>) and re-classified. Every leftover item (whichever
    /// list is still non-empty once the other empties) is exact multiples of the total and always
    /// keeps itself. Because the arithmetic is exact, this differs from the textbook
    /// floating-point Vose construction only in that there is no rounding residue to sweep up —
    /// the final "small" drain loop exists purely as a defensive no-op for that reason.
    /// </remarks>
    private static WeightedSampler<T> BuildAliasTable(T[] items, long[] weights, long total)
    {
        int n = items.Length;
        var threshold = new long[n];
        var alias = new int[n];

        if (n == 1)
        {
            // A single item always keeps itself; scale/total classification below would also
            // reach this result, but skipping it avoids the n == 1 edge on the Stack-based loop.
            threshold[0] = total;
            alias[0] = 0;
            return new WeightedSampler<T>(items, threshold, alias, total);
        }

        var scaled = new Int128[n];
        var small = new Stack<int>();
        var large = new Stack<int>();
        for (int i = 0; i < n; i++)
        {
            scaled[i] = (Int128)n * weights[i];
            if (scaled[i] < total)
            {
                small.Push(i);
            }
            else
            {
                large.Push(i);
            }
        }

        while (small.Count > 0 && large.Count > 0)
        {
            int l = small.Pop();
            int g = large.Pop();

            threshold[l] = (long)scaled[l];
            alias[l] = g;

            scaled[g] = scaled[g] + scaled[l] - total;
            if (scaled[g] < total)
            {
                small.Push(g);
            }
            else
            {
                large.Push(g);
            }
        }

        while (large.Count > 0)
        {
            threshold[large.Pop()] = total;
        }

        while (small.Count > 0)
        {
            // Reachable only through floating-point rounding in the classic construction; with
            // exact integer arithmetic this loop should never execute, but it is kept as a safe
            // fallback (self-select is always a correct answer, it just forgoes alias sharing).
            threshold[small.Pop()] = total;
        }

        return new WeightedSampler<T>(items, threshold, alias, total);
    }

    /// <summary>
    /// Draws a single random element, selected with probability proportional to the weight it was
    /// built with.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <returns>A weighted-randomly selected element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public T Pick(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var generator = new SourceGenerator(source);
        int column = (int)RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)_items.Length);
        long offset = (long)RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)_total);

        return offset < _threshold[column] ? _items[column] : _items[_alias[column]];
    }

    /// <summary>
    /// Draws <paramref name="count"/> elements with replacement (the same item may be drawn more
    /// than once), each draw independently weighted as in <see cref="Pick(IRandomSource)"/>.
    /// </summary>
    /// <param name="source">The random source.</param>
    /// <param name="count">The number of elements to draw. Must be greater than 0.</param>
    /// <returns>An array of <paramref name="count"/> weighted-randomly selected elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not greater than 0.</exception>
    public T[] PickMany(IRandomSource source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be greater than zero.");
        }

        var result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = Pick(source);
        }

        return result;
    }
}
