namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §4.7 / §6 test 6: "generated <c>ComputeStableHash()</c> is a stateless pure
/// function -- thread safe." This is a smoke test, not a proof: it calls
/// <c>ComputeStableHash()</c> on one shared instance from many threads concurrently and asserts
/// every result is identical, which would very likely fail (flakily) if the generated code or
/// <see cref="StableHashWriter"/> secretly depended on shared mutable state.
/// </summary>
public class ThreadSafetyTests
{
    [Fact]
    public void ComprehensiveContract_ComputeStableHash_FromManyThreadsConcurrently_AllProduceTheSameHash()
    {
        ComprehensiveContract value = TestFixtures.BuildComprehensiveInstance();
        StableHash64 expected = value.ComputeStableHash();

        const int threadCount = 32;
        var results = new StableHash64[threadCount];

        Parallel.For(0, threadCount, i =>
        {
            results[i] = value.ComputeStableHash();
        });

        Assert.All(results, actual => Assert.Equal(expected, actual));
    }

    [Fact]
    public void PositionStructContract_ComputeStableHash_FromManyThreadsConcurrently_AllProduceTheSameHash()
    {
        var value = new Position { X = 123, Y = -456 };
        StableHash64 expected = value.ComputeStableHash();

        const int threadCount = 32;
        var results = new StableHash64[threadCount];

        Parallel.For(0, threadCount, i =>
        {
            results[i] = value.ComputeStableHash();
        });

        Assert.All(results, actual => Assert.Equal(expected, actual));
    }
}
