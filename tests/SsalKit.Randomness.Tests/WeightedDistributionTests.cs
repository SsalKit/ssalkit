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

        // Use the alias method's observed counts (scaled to the same sample size, trivially
        // already equal here) as the "expected" distribution for the cumulative-sum method's
        // chi-square test, and vice versa is unnecessary since the test is symmetric in spirit.
        double[] aliasAsExpected = new double[aliasCounts.Length];
        for (int i = 0; i < aliasCounts.Length; i++)
        {
            aliasAsExpected[i] = aliasCounts[i];
        }

        double chiSquare = ChiSquare(cumulativeSumCounts, aliasAsExpected);

        Assert.True(chiSquare < CriticalValue, $"chi-square statistic {chiSquare} exceeded critical value {CriticalValue}; cumulativeSumCounts=[{string.Join(", ", cumulativeSumCounts)}], aliasCounts=[{string.Join(", ", aliasCounts)}]");
    }
}
