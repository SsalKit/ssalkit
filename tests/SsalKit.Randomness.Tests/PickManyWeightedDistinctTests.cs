namespace SsalKit.Randomness.Tests;

/// <summary>
/// Functional tests for <see cref="WeightedRandomExtensions.PickManyWeightedDistinct{T}(IRandomSource, IReadOnlyList{T}, Func{T, long}, int)"/>
/// (without replacement): no duplicates, zero-weight items never appear, drawing exactly the
/// number of positive-weight items returns all of them (in selection order), count-boundary
/// exceptions, and reproducibility under a fixed seed.
/// </summary>
public class PickManyWeightedDistinctTests
{
    private static readonly string[] Items = ["a", "b", "c", "d", "e", "f", "g", "h"];
    private static readonly long[] Weights = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public void PickManyWeightedDistinct_NeverReturnsDuplicates()
    {
        var random = new DeterministicRandom(1UL);
        string[] result = random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], Items.Length);

        Assert.Equal(result.Length, result.Distinct().Count());
    }

    [Fact]
    public void PickManyWeightedDistinct_PartialDraw_NeverReturnsDuplicates()
    {
        var random = new DeterministicRandom(2UL);
        string[] result = random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 5);

        Assert.Equal(5, result.Length);
        Assert.Equal(result.Length, result.Distinct().Count());
    }

    [Fact]
    public void PickManyWeightedDistinct_ZeroWeightItem_NeverAppears()
    {
        string[] items = ["a", "z1", "b", "z2", "c"];
        long[] weights = [10, 0, 10, 0, 10];
        var random = new DeterministicRandom(3UL);

        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 3);

        Assert.DoesNotContain("z1", result);
        Assert.DoesNotContain("z2", result);
        Assert.Equal(3, result.Distinct().Count());
    }

    [Fact]
    public void PickManyWeightedDistinct_CountEqualsPositiveWeightCount_ReturnsAllPositiveWeightItems()
    {
        string[] items = ["a", "z", "b", "c"];
        long[] weights = [1, 0, 1, 1];
        var random = new DeterministicRandom(4UL);

        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 3);

        Assert.Equal(["a", "b", "c"], result.OrderBy(x => x, StringComparer.Ordinal));
        Assert.DoesNotContain("z", result);
    }

    [Fact]
    public void PickManyWeightedDistinct_ResultOrderIsSelectionOrder_NotSorted()
    {
        // With a heavily skewed weight distribution and a fixed seed, the highest-weight item
        // should be selected first far more often than not; assert the raw (unsorted) order is
        // preserved by checking it can differ from sorted order across repeated distinct seeds.
        string[] items = ["low", "high"];
        long[] weights = [1, 1_000_000];

        var random = new DeterministicRandom(5UL);
        string[] result = random.PickManyWeightedDistinct(items, x => weights[Array.IndexOf(items, x)], 2);

        Assert.Equal("high", result[0]);
        Assert.Equal("low", result[1]);
    }

    [Fact]
    public void PickManyWeightedDistinct_SameSeed_IsReproducible()
    {
        var a = new DeterministicRandom(6UL);
        var b = new DeterministicRandom(6UL);

        string[] resultA = a.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 5);
        string[] resultB = b.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 5);

        Assert.Equal(resultA, resultB);
    }

    [Fact]
    public void PickManyWeightedDistinct_CountZero_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], 0));
    }

    [Fact]
    public void PickManyWeightedDistinct_CountExceedsItemCount_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.PickManyWeightedDistinct(Items, x => Weights[Array.IndexOf(Items, x)], Items.Length + 1));
    }

    [Fact]
    public void PickManyWeightedDistinct_SingleItem_ReturnsIt()
    {
        var random = new DeterministicRandom(1UL);
        string[] result = random.PickManyWeightedDistinct(["only"], static _ => 1L, 1);

        Assert.Equal(["only"], result);
    }

    // -----------------------------------------------------------------
    // Distribution: sequential (successive-sampling) semantics
    //
    // Without replacement, only the first draw is proportional to the weights; every later draw
    // renormalizes over what is left. The tests below pin that exact model rather than the
    // "inclusion probability is proportional to weight" model people usually assume, which this
    // method deliberately does not implement.
    // -----------------------------------------------------------------

    private static readonly string[] FourItems = ["a", "b", "c", "d"];
    private static readonly long[] FourWeights = [1, 2, 3, 4];

    private const int PairSampleCount = 120_000;

    /// <summary>
    /// Chi-square critical value for 5 degrees of freedom (6 unordered pairs minus 1) at the same
    /// deliberately generous <c>p = 0.0001</c> significance level <c>WeightedDistributionTests</c>
    /// uses, so a correct implementation cannot fail this even on an unlucky seed.
    /// </summary>
    private const double PairCriticalValue = 25.74;

    /// <summary>
    /// One simulation run of <c>PickManyWeightedDistinct(count: 2)</c> over
    /// <see cref="FourItems"/>/<see cref="FourWeights"/>, shared by the tests below so the draws
    /// happen once. The seed is fixed, so both the counts and every assertion on them are
    /// reproducible.
    /// </summary>
    private static readonly (int[] Pairs, int[] Inclusions) PairSample = SimulatePairDraws(0xD15C0UL, PairSampleCount);

    private static (int[] Pairs, int[] Inclusions) SimulatePairDraws(ulong seed, int sampleCount)
    {
        var random = new DeterministicRandom(seed);
        var pairs = new int[6];
        var inclusions = new int[FourItems.Length];

        for (int i = 0; i < sampleCount; i++)
        {
            string[] drawn = random.PickManyWeightedDistinct(FourItems, static x => FourWeights[Array.IndexOf(FourItems, x)], 2);
            int first = Array.IndexOf(FourItems, drawn[0]);
            int second = Array.IndexOf(FourItems, drawn[1]);

            pairs[PairIndex(first, second)]++;
            inclusions[first]++;
            inclusions[second]++;
        }

        return (pairs, inclusions);
    }

    /// <summary>
    /// Maps an unordered pair of distinct item indices onto <c>[0, 6)</c> in the order
    /// <c>{0,1}, {0,2}, {0,3}, {1,2}, {1,3}, {2,3}</c>.
    /// </summary>
    private static int PairIndex(int first, int second)
    {
        int low = Math.Min(first, second);
        int high = Math.Max(first, second);

        return low switch
        {
            0 => high - 1,
            1 => high + 1,
            _ => 5,
        };
    }

    /// <summary>
    /// The exact probability that a size-2 draw without replacement yields the unordered pair
    /// <c>{first, second}</c> under sequential semantics: either item can come out first, and the
    /// second draw renormalizes over the total minus whatever was taken.
    /// </summary>
    private static double ExactPairProbability(int first, int second)
    {
        double total = 0;
        foreach (long w in FourWeights)
        {
            total += w;
        }

        return ((FourWeights[first] / total) * (FourWeights[second] / (total - FourWeights[first])))
            + ((FourWeights[second] / total) * (FourWeights[first] / (total - FourWeights[second])));
    }

    /// <summary>
    /// The exact probability that <paramref name="item"/> appears anywhere in a size-2 draw: the
    /// sum of the three pair probabilities that contain it.
    /// </summary>
    private static double ExactInclusionProbability(int item)
    {
        double probability = 0;
        for (int other = 0; other < FourWeights.Length; other++)
        {
            if (other != item)
            {
                probability += ExactPairProbability(item, other);
            }
        }

        return probability;
    }

    [Fact]
    public void PickManyWeightedDistinct_PairFrequencies_MatchSequentialDrawTheory()
    {
        double chiSquare = 0.0;
        for (int first = 0; first < FourItems.Length; first++)
        {
            for (int second = first + 1; second < FourItems.Length; second++)
            {
                double expected = PairSampleCount * ExactPairProbability(first, second);
                double diff = PairSample.Pairs[PairIndex(first, second)] - expected;
                chiSquare += (diff * diff) / expected;
            }
        }

        Assert.True(
            chiSquare < PairCriticalValue,
            $"chi-square statistic {chiSquare} exceeded critical value {PairCriticalValue}; pairCounts=[{string.Join(", ", PairSample.Pairs)}]");
    }

    [Theory]
    // The exact sequential-draw inclusion probabilities for weights [1, 2, 3, 4] and count = 2.
    [InlineData(0, 197.0 / 840.0)]
    [InlineData(1, 139.0 / 315.0)]
    [InlineData(2, 73.0 / 120.0)]
    [InlineData(3, 451.0 / 630.0)]
    public void PickManyWeightedDistinct_InclusionFrequencies_MatchSequentialDrawTheory(int item, double expectedProbability)
    {
        // The closed forms in the InlineData rows and the pair-sum computation are two independent
        // derivations of the same number; agreeing pins both.
        Assert.Equal(expectedProbability, ExactInclusionProbability(item), 12);

        double observed = PairSample.Inclusions[item] / (double)PairSampleCount;

        // Five standard errors of a binomial proportion at this sample size is under 0.008 for
        // every row, so this tolerance is loose enough never to flake and far tighter than the gap
        // to the weight-proportional value the next test rules out.
        Assert.InRange(observed, expectedProbability - 0.008, expectedProbability + 0.008);
    }

    [Fact]
    public void PickManyWeightedDistinct_InclusionProbability_IsNotProportionalToWeight()
    {
        // The documented caveat, asserted rather than only written down: drawing 2 of 4 does NOT
        // make an item's chance of appearing equal to count * weight / total. The lightest item
        // comes out well above its weight share and the heaviest well below, because a heavy item
        // that has already been drawn can no longer crowd the others out.
        double totalWeight = 0;
        foreach (long w in FourWeights)
        {
            totalWeight += w;
        }

        double lightestObserved = PairSample.Inclusions[0] / (double)PairSampleCount;
        double lightestProportional = 2 * (FourWeights[0] / totalWeight);
        Assert.True(
            lightestObserved > lightestProportional + 0.02,
            $"expected the lightest item to be over-represented; observed {lightestObserved} vs weight-proportional {lightestProportional}");

        double heaviestObserved = PairSample.Inclusions[3] / (double)PairSampleCount;
        double heaviestProportional = 2 * (FourWeights[3] / totalWeight);
        Assert.True(
            heaviestObserved < heaviestProportional - 0.02,
            $"expected the heaviest item to be under-represented; observed {heaviestObserved} vs weight-proportional {heaviestProportional}");
    }
}
