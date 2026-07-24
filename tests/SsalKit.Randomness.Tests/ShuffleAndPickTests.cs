namespace SsalKit.Randomness.Tests;

public class ShuffleAndPickTests
{
    // All 6 permutations of {0, 1, 2}, used to rank a shuffled 3-element array into a bucket
    // index for the chi-square uniformity smoke test below.
    private static readonly int[][] Permutations3 =
    [
        [0, 1, 2],
        [0, 2, 1],
        [1, 0, 2],
        [1, 2, 0],
        [2, 0, 1],
        [2, 1, 0],
    ];

    private static int IndexOfPermutation(ReadOnlySpan<int> arr)
    {
        for (int i = 0; i < Permutations3.Length; i++)
        {
            if (Permutations3[i][0] == arr[0] && Permutations3[i][1] == arr[1] && Permutations3[i][2] == arr[2])
            {
                return i;
            }
        }

        throw new InvalidOperationException("arr is not a permutation of {0, 1, 2}.");
    }

    [Fact]
    public void Shuffle_Span_SameSeed_IsReproducible()
    {
        int[] a = [1, 2, 3, 4, 5, 6, 7, 8];
        int[] b = [1, 2, 3, 4, 5, 6, 7, 8];

        new DeterministicRandom(9001UL).Shuffle(a.AsSpan());
        new DeterministicRandom(9001UL).Shuffle(b.AsSpan());

        Assert.Equal(a, b);
    }

    [Fact]
    public void Shuffle_Span_PreservesMultiset()
    {
        int[] original = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        int[] shuffled = [.. original];

        new DeterministicRandom(123UL).Shuffle(shuffled.AsSpan());

        Assert.Equal(original.OrderBy(x => x), shuffled.OrderBy(x => x));
    }

    [Fact]
    public void Shuffle_Span_LengthZero_DoesNotThrow()
    {
        var random = new DeterministicRandom(1UL);
        Span<int> values = [];

        random.Shuffle(values);
    }

    [Fact]
    public void Shuffle_Span_LengthOne_DoesNotThrow()
    {
        var random = new DeterministicRandom(1UL);
        Span<int> values = [42];

        random.Shuffle(values);

        Assert.Equal(42, values[0]);
    }

    [Fact]
    public void Shuffle_IList_SameSeed_IsReproducible()
    {
        List<int> a = [1, 2, 3, 4, 5, 6, 7, 8];
        List<int> b = [1, 2, 3, 4, 5, 6, 7, 8];

        new DeterministicRandom(9001UL).Shuffle(a);
        new DeterministicRandom(9001UL).Shuffle(b);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Shuffle_IList_NullList_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentNullException>(() => random.Shuffle((IList<int>)null!));
    }

    [Fact]
    public void Shuffle_SpanAndIListPaths_ProduceIdenticalResultsForSameState()
    {
        int[] viaSpan = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        List<int> viaList = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        new DeterministicRandom(4242UL).Shuffle(viaSpan.AsSpan());
        new DeterministicRandom(4242UL).Shuffle(viaList);

        Assert.Equal(viaSpan, viaList);
    }

    [Fact]
    public void Shuffle_ThreeElements_IsUnbiased_ChiSquareSmokeTest()
    {
        // Six possible permutations of a 3-element array, large sample, chi-square
        // goodness-of-fit against a uniform distribution over permutations. Degrees of freedom
        // = 5 (permutations - 1). Critical value for a very generous significance level
        // (p = 0.0001, chi2 = 25.74) is used to avoid flakiness while still catching gross bias.
        const int sampleCount = 60_000;
        const double criticalValue = 25.74;

        var random = new DeterministicRandom(0xBEEFUL);
        var counts = new int[Permutations3.Length];
        for (int i = 0; i < sampleCount; i++)
        {
            Span<int> values = [0, 1, 2];
            random.Shuffle(values);
            counts[IndexOfPermutation(values)]++;
        }

        double expected = sampleCount / (double)Permutations3.Length;
        double chiSquare = 0.0;
        foreach (int count in counts)
        {
            double diff = count - expected;
            chiSquare += (diff * diff) / expected;
        }

        Assert.True(chiSquare < criticalValue, $"chi-square statistic {chiSquare} exceeded critical value {criticalValue}; counts=[{string.Join(", ", counts)}]");
    }

    [Fact]
    public void Pick_Span_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentException>(() => random.Pick(ReadOnlySpan<int>.Empty));
    }

    [Fact]
    public void Pick_IReadOnlyList_EmptyItems_ThrowsArgumentException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentException>(() => random.Pick((IReadOnlyList<int>)[]));
    }

    [Fact]
    public void Pick_IReadOnlyList_NullItems_ThrowsArgumentNullException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentNullException>(() => random.Pick((IReadOnlyList<int>)null!));
    }

    [Fact]
    public void Pick_Span_SingleElement_AlwaysReturnsIt()
    {
        var random = new DeterministicRandom(1UL);
        ReadOnlySpan<string> items = ["only"];

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal("only", random.Pick(items));
        }
    }

    [Fact]
    public void Pick_IReadOnlyList_SingleElement_AlwaysReturnsIt()
    {
        var random = new DeterministicRandom(1UL);
        IReadOnlyList<string> items = ["only"];

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal("only", random.Pick(items));
        }
    }

    [Fact]
    public void Pick_Span_SameSeed_IsReproducible()
    {
        ReadOnlySpan<string> items = ["a", "b", "c", "d", "e"];

        var a = new DeterministicRandom(555UL);
        var b = new DeterministicRandom(555UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.Pick(items), b.Pick(items));
        }
    }

    [Fact]
    public void Pick_IReadOnlyList_SameSeed_IsReproducible()
    {
        IReadOnlyList<string> items = ["a", "b", "c", "d", "e"];

        var a = new DeterministicRandom(555UL);
        var b = new DeterministicRandom(555UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.Pick(items), b.Pick(items));
        }
    }

    [Fact]
    public void Pick_Span_And_IReadOnlyList_AgreeForSameState()
    {
        ReadOnlySpan<string> viaSpan = ["a", "b", "c", "d", "e"];
        IReadOnlyList<string> viaList = ["a", "b", "c", "d", "e"];

        var forSpan = new DeterministicRandom(2024UL);
        var forList = new DeterministicRandom(2024UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(forSpan.Pick(viaSpan), forList.Pick(viaList));
        }
    }
}
