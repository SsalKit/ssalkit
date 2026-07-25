namespace SsalKit.Randomness.Tests;

/// <summary>
/// Derivation-consistency tests (design doc §6-7): for identical starting state, every
/// <see cref="DeterministicRandom"/> instance method must return exactly the same sequence of
/// values as the corresponding <see cref="RandomSourceExtensions"/> extension method called
/// through the <see cref="IRandomSource"/> interface. This is the core guarantee that lets
/// higher-level code accept the interface without behavioral drift from the concrete type.
/// </summary>
public class RandomSourceExtensionsAgreementTests
{
    [Fact]
    public void Next_NoArguments_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(123UL);
        IRandomSource viaInterface = new DeterministicRandom(123UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.Next(), viaInterface.Next());
        }
    }

    [Fact]
    public void Next_WithMaxValue_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(456UL);
        IRandomSource viaInterface = new DeterministicRandom(456UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.Next(37), viaInterface.Next(37));
        }
    }

    [Fact]
    public void Next_WithMinAndMax_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(789UL);
        IRandomSource viaInterface = new DeterministicRandom(789UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.Next(-20, 55), viaInterface.Next(-20, 55));
        }
    }

    [Fact]
    public void NextInt64_NoArguments_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(111UL);
        IRandomSource viaInterface = new DeterministicRandom(111UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.NextInt64(), viaInterface.NextInt64());
        }
    }

    [Fact]
    public void NextInt64_WithMaxValue_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(222UL);
        IRandomSource viaInterface = new DeterministicRandom(222UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.NextInt64(1000L), viaInterface.NextInt64(1000L));
        }
    }

    [Fact]
    public void NextInt64_WithMinAndMax_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(333UL);
        IRandomSource viaInterface = new DeterministicRandom(333UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.NextInt64(long.MinValue, long.MaxValue), viaInterface.NextInt64(long.MinValue, long.MaxValue));
        }
    }

    [Fact]
    public void NextDouble_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(444UL);
        IRandomSource viaInterface = new DeterministicRandom(444UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.NextDouble(), viaInterface.NextDouble());
        }
    }

    [Fact]
    public void NextSingle_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(555UL);
        IRandomSource viaInterface = new DeterministicRandom(555UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.NextSingle(), viaInterface.NextSingle());
        }
    }

    [Fact]
    public void NextBoolean_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(666UL);
        IRandomSource viaInterface = new DeterministicRandom(666UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(instance.NextBoolean(), viaInterface.NextBoolean());
        }
    }

    [Fact]
    public void Next_ArgumentValidation_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(1UL);
        IRandomSource viaInterface = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => instance.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => viaInterface.Next(-1));

        Assert.Throws<ArgumentOutOfRangeException>(() => instance.Next(5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => viaInterface.Next(5, 4));

        Assert.Equal(instance.Next(42, 42), viaInterface.Next(42, 42));
    }

    [Fact]
    public void NextInt64_ArgumentValidation_MatchesInstanceMethod()
    {
        var instance = new DeterministicRandom(1UL);
        IRandomSource viaInterface = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => instance.NextInt64(-1L));
        Assert.Throws<ArgumentOutOfRangeException>(() => viaInterface.NextInt64(-1L));

        Assert.Throws<ArgumentOutOfRangeException>(() => instance.NextInt64(5L, 4L));
        Assert.Throws<ArgumentOutOfRangeException>(() => viaInterface.NextInt64(5L, 4L));

        Assert.Equal(instance.NextInt64(42L, 42L), viaInterface.NextInt64(42L, 42L));
    }

    [Fact]
    public void Next_WithMaxValueZero_ViaExtensionMethod_ReturnsZero()
    {
        // Called through the IRandomSource-typed local so overload resolution binds to
        // RandomSourceExtensions.Next(this IRandomSource, int), not DeterministicRandom's own
        // instance method of the same signature.
        IRandomSource viaInterface = new DeterministicRandom(1UL);

        Assert.Equal(0, viaInterface.Next(0));
    }

    [Fact]
    public void NextInt64_WithMaxValueZero_ViaExtensionMethod_ReturnsZero()
    {
        // Called through the IRandomSource-typed local so overload resolution binds to
        // RandomSourceExtensions.NextInt64(this IRandomSource, long), not DeterministicRandom's
        // own instance method of the same signature.
        IRandomSource viaInterface = new DeterministicRandom(1UL);

        Assert.Equal(0L, viaInterface.NextInt64(0L));
    }
}
