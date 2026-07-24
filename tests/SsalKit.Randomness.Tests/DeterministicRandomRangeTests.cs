namespace SsalKit.Randomness.Tests;

public class DeterministicRandomRangeTests
{
    [Fact]
    public void Next_NoArguments_NeverReturnsIntMaxValue()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 100_000; i++)
        {
            int value = random.Next();
            Assert.True(value >= 0 && value < int.MaxValue);
        }
    }

    [Fact]
    public void Next_MaxValueZero_ReturnsZero()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Equal(0, random.Next(0));
    }

    [Fact]
    public void Next_NegativeMaxValue_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
    }

    [Fact]
    public void Next_WithMaxValue_StaysWithinBounds()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 50_000; i++)
        {
            int value = random.Next(10);
            Assert.True(value >= 0 && value < 10);
        }
    }

    [Fact]
    public void Next_MinEqualsMax_ReturnsMin()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Equal(42, random.Next(42, 42));
        Assert.Equal(-42, random.Next(-42, -42));
    }

    [Fact]
    public void Next_MinGreaterThanMax_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5, 4));
    }

    [Fact]
    public void Next_NegativeRange_StaysWithinBounds()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 50_000; i++)
        {
            int value = random.Next(-10, -5);
            Assert.True(value >= -10 && value < -5);
        }
    }

    [Fact]
    public void Next_FullIntRange_StaysWithinBoundsAndProducesNegativeValues()
    {
        var random = new DeterministicRandom(1UL);
        bool sawNegative = false;
        bool sawPositive = false;
        for (int i = 0; i < 50_000; i++)
        {
            int value = random.Next(int.MinValue, int.MaxValue);
            Assert.True(value >= int.MinValue && value < int.MaxValue);
            sawNegative |= value < 0;
            sawPositive |= value > 0;
        }

        Assert.True(sawNegative);
        Assert.True(sawPositive);
    }

    [Fact]
    public void NextInt64_NoArguments_NeverReturnsLongMaxValue()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 100_000; i++)
        {
            long value = random.NextInt64();
            Assert.True(value >= 0 && value < long.MaxValue);
        }
    }

    [Fact]
    public void NextInt64_MaxValueZero_ReturnsZero()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Equal(0L, random.NextInt64(0));
    }

    [Fact]
    public void NextInt64_NegativeMaxValue_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt64(-1));
    }

    [Fact]
    public void NextInt64_WithMaxValue_StaysWithinBounds()
    {
        var random = new DeterministicRandom(1UL);
        for (int i = 0; i < 50_000; i++)
        {
            long value = random.NextInt64(10L);
            Assert.True(value >= 0 && value < 10);
        }
    }

    [Fact]
    public void NextInt64_MinEqualsMax_ReturnsMin()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Equal(42L, random.NextInt64(42L, 42L));
    }

    [Fact]
    public void NextInt64_MinGreaterThanMax_ThrowsArgumentOutOfRangeException()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt64(5L, 4L));
    }

    [Fact]
    public void NextInt64_FullLongRange_StaysWithinBoundsAndProducesNegativeValues()
    {
        // The extreme case: exclusiveRange = 2^64 - 1, which overflows signed 64-bit arithmetic
        // if computed naively. Exercises the unchecked ulong subtraction/addition path.
        var random = new DeterministicRandom(1UL);
        bool sawNegative = false;
        bool sawPositive = false;
        for (int i = 0; i < 50_000; i++)
        {
            long value = random.NextInt64(long.MinValue, long.MaxValue);
            Assert.True(value >= long.MinValue && value < long.MaxValue);
            sawNegative |= value < 0;
            sawPositive |= value > 0;
        }

        Assert.True(sawNegative);
        Assert.True(sawPositive);
    }

    [Fact]
    public void Next_SmallRange_IsUnbiased_ChiSquareSmokeTest()
    {
        // Small range (3), large sample, chi-square goodness-of-fit against a uniform
        // distribution. Degrees of freedom = 2 (bins - 1). Critical value for a very generous
        // significance level (p = 0.0001, chi2 = 18.42) is used to avoid flakiness while still
        // catching gross modulo-style bias.
        const int range = 3;
        const int sampleCount = 300_000;
        const double criticalValue = 18.42;

        var random = new DeterministicRandom(0xC0FFEEUL);
        var counts = new int[range];
        for (int i = 0; i < sampleCount; i++)
        {
            counts[random.Next(range)]++;
        }

        double expected = sampleCount / (double)range;
        double chiSquare = 0.0;
        foreach (int count in counts)
        {
            double diff = count - expected;
            chiSquare += (diff * diff) / expected;
        }

        Assert.True(chiSquare < criticalValue, $"chi-square statistic {chiSquare} exceeded critical value {criticalValue}; counts=[{string.Join(", ", counts)}]");
    }

    [Fact]
    public void Next_And_RandomAlgorithmsNextUInt64Bounded_AgreeForSameState()
    {
        // The DeterministicRandom.Next(int) instance method must delegate to exactly the same
        // Lemire logic that RandomAlgorithms.NextUInt64Bounded implements, so the two never
        // diverge for equivalent starting states.
        var forNextMethod = new DeterministicRandom(555UL);
        var forHelper = new DeterministicRandom(555UL);
        var helperGenerator = new DeterministicRandomGenerator(forHelper);

        for (int i = 0; i < 1_000; i++)
        {
            int viaMethod = forNextMethod.Next(100);
            int viaHelper = (int)RandomAlgorithms.NextUInt64Bounded(ref helperGenerator, 100UL);
            Assert.Equal(viaMethod, viaHelper);
        }
    }

    /// <summary>
    /// Adapter wrapping a real <see cref="DeterministicRandom"/> instance via its public
    /// <see cref="DeterministicRandom.NextUInt64"/> method, so tests can drive
    /// <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/> directly from a genuine
    /// xoshiro256** sequence.
    /// </summary>
    private readonly struct DeterministicRandomGenerator(DeterministicRandom random) : IUInt64Generator
    {
        public ulong NextUInt64() => random.NextUInt64();
    }
}
