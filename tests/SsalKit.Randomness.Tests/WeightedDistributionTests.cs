namespace SsalKit.Randomness.Tests;

/// <summary>
/// Chi-square goodness-of-fit smoke tests confirming <see cref="WeightedRandomExtensions.PickWeighted{T}(IRandomSource, ReadOnlySpan{T}, ReadOnlySpan{long})"/>
/// and <see cref="WeightedSampler{T}.Pick(IRandomSource)"/> both select items with frequency
/// proportional to their weight, and that the two independent implementations (cumulative-sum
/// binary search vs. Walker/Vose alias table) approximate each other, not just the expected
/// ratio individually — mirroring the chi-square style used for <c>Shuffle</c> in
/// <c>ShuffleAndPickTests</c>.
/// </summary>
public class WeightedDistributionTests
{
    private static readonly string[] Items = ["a", "b", "c", "d"];
    private static readonly long[] Weights = [1, 2, 3, 4];
    private const int SampleCount = 120_000;

    // Degrees of freedom = 3 (4 buckets - 1). Critical value for a very generous significance
    // level (p = 0.0001, chi2 = 21.11) is used to avoid flakiness while still catching gross bias.
    private const double CriticalValue = 21.11;

    private static double ChiSquare(int[] observed, double[] expected)
    {
        double chiSquare = 0.0;
        for (int i = 0; i < observed.Length; i++)
        {
            double diff = observed[i] - expected[i];
            chiSquare += (diff * diff) / expected[i];
        }

        return chiSquare;
    }

    private static double[] ExpectedCounts(long[] weights, int sampleCount)
    {
        long total = 0;
        foreach (long w in weights)
        {
            total += w;
        }

        var expected = new double[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            expected[i] = sampleCount * (weights[i] / (double)total);
        }

        return expected;
    }

    private static int[] CountSelections(Func<int> drawIndex, int sampleCount)
    {
        var counts = new int[Items.Length];
        for (int i = 0; i < sampleCount; i++)
        {
            counts[drawIndex()]++;
        }

        return counts;
    }

    [Fact]
    public void PickWeighted_Span_MatchesExpectedRatio_ChiSquareSmokeTest()
    {
        var random = new DeterministicRandom(0xC0FFEEUL);
        int[] observed = CountSelections(() => Array.IndexOf(Items, random.PickWeighted(Items.AsSpan(), Weights.AsSpan())), SampleCount);

        double[] expected = ExpectedCounts(Weights, SampleCount);
        double chiSquare = ChiSquare(observed, expected);

        Assert.True(chiSquare < CriticalValue, $"chi-square statistic {chiSquare} exceeded critical value {CriticalValue}; counts=[{string.Join(", ", observed)}]");
    }

    [Fact]
    public void WeightedSampler_Pick_MatchesExpectedRatio_ChiSquareSmokeTest()
    {
        var sampler = WeightedSampler<string>.Create(Items.AsSpan(), Weights.AsSpan());
        var random = new DeterministicRandom(0xC0FFEEUL);
        int[] observed = CountSelections(() => Array.IndexOf(Items, sampler.Pick(random)), SampleCount);

        double[] expected = ExpectedCounts(Weights, SampleCount);
        double chiSquare = ChiSquare(observed, expected);

        Assert.True(chiSquare < CriticalValue, $"chi-square statistic {chiSquare} exceeded critical value {CriticalValue}; counts=[{string.Join(", ", observed)}]");
    }

    [Fact]
    public void PickWeighted_Span_MatchesExpectedRatio_Delegate_ChiSquareSmokeTest()
    {
        var random = new DeterministicRandom(0xBADA55UL);
        int[] observed = CountSelections(() => Array.IndexOf(Items, random.PickWeighted(Items, static x => Weights[Array.IndexOf(Items, x)])), SampleCount);

        double[] expected = ExpectedCounts(Weights, SampleCount);
        double chiSquare = ChiSquare(observed, expected);

        Assert.True(chiSquare < CriticalValue, $"chi-square statistic {chiSquare} exceeded critical value {CriticalValue}; counts=[{string.Join(", ", observed)}]");
    }

    /// <summary>
    /// The chi-square statistic for the homogeneity of two independent samples of equal size:
    /// <c>sum (a[i] - b[i])^2 / (a[i] + b[i])</c>, which is asymptotically chi-square with
    /// <c>k - 1</c> degrees of freedom.
    /// </summary>
    /// <remarks>
    /// Not the same statistic as <see cref="ChiSquare"/>, which compares one observed sample
    /// against a *known* expectation. Feeding one sample's counts in as the other's "expected"
    /// would double-count the sampling noise — both sides fluctuate, so the resulting statistic is
    /// roughly twice as large as a one-sample statistic and must not be compared against a
    /// one-sample critical value. Dividing by <c>a[i] + b[i]</c> rather than <c>b[i]</c> is exactly
    /// that factor-of-two correction, and keeps <see cref="CriticalValue"/> meaningful here.
    /// </remarks>
    private static double TwoSampleChiSquare(int[] a, int[] b)
    {
        double chiSquare = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            double diff = a[i] - b[i];
            chiSquare += (diff * diff) / (a[i] + b[i]);
        }

        return chiSquare;
    }

    [Fact]
    public void PickWeighted_AndWeightedSampler_ApproximateEachOther_ChiSquareSmokeTest()
    {
        // Cross-check the two independent implementations (cumulative-sum binary search vs.
        // Walker/Vose alias table) against each other's *observed* distribution, not just each
        // against the theoretical ratio, so a shared systematic bias (unlikely, but not ruled out
        // by the two tests above individually) would still be caught if one implementation
        // diverges from the other.
        var cumulativeSumRandom = new DeterministicRandom(1UL);
        int[] cumulativeSumCounts = CountSelections(
            () => Array.IndexOf(Items, cumulativeSumRandom.PickWeighted(Items.AsSpan(), Weights.AsSpan())),
            SampleCount);

        var sampler = WeightedSampler<string>.Create(Items.AsSpan(), Weights.AsSpan());
        var aliasRandom = new DeterministicRandom(2UL);
        int[] aliasCounts = CountSelections(() => Array.IndexOf(Items, sampler.Pick(aliasRandom)), SampleCount);

        double chiSquare = TwoSampleChiSquare(cumulativeSumCounts, aliasCounts);

        Assert.True(chiSquare < CriticalValue, $"two-sample chi-square statistic {chiSquare} exceeded critical value {CriticalValue}; cumulativeSumCounts=[{string.Join(", ", cumulativeSumCounts)}], aliasCounts=[{string.Join(", ", aliasCounts)}]");
    }
}
