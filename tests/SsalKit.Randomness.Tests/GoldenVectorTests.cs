namespace SsalKit.Randomness.Tests;

/// <summary>
/// Pins the xoshiro256** + SplitMix64 algorithm contract (design doc §4.1): the same seed must
/// always produce the same sequence. Verified two ways: against an independently transcribed
/// reference implementation (<see cref="ReferenceXoshiro256StarStar"/>), and against literal
/// hardcoded values that do not depend on the reference implementation at all, so an accidental
/// change to both in the same way is still caught.
/// </summary>
public class GoldenVectorTests
{
    public static TheoryData<ulong> Seeds => new()
    {
        0UL,
        1UL,
        42UL,
        ulong.MaxValue,
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void NextUInt64_MatchesIndependentReferenceImplementation_First32Outputs(ulong seed)
    {
        ulong[] expected = ReferenceXoshiro256StarStar.NextUInt64Sequence(seed, 32);

        var random = new DeterministicRandom(seed);
        var actual = new ulong[32];
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = random.NextUInt64();
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NextUInt64_Seed42_MatchesHardcodedLiteralFirst8Outputs()
    {
        // Hardcoded independently of ReferenceXoshiro256StarStar, computed once from the same
        // public xoshiro256**/SplitMix64 algorithm definitions and pinned here as a literal
        // regression guard: even if the reference transcription above were accidentally broken
        // in the same way as production code, this test would still catch a sequence change.
        ulong[] expected =
        [
            0x15780B2E0C2EC716UL,
            0x6104D9866D113A7EUL,
            0xAE17533239E499A1UL,
            0xECB8AD4703B360A1UL,
            0xFDE6DC7FE2EC5E64UL,
            0xC50DA53101795238UL,
            0xB82154855A65DDB2UL,
            0xD99A2743EBE60087UL,
        ];

        var random = new DeterministicRandom(42UL);
        var actual = new ulong[8];
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = random.NextUInt64();
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SplitMix64_Seed0x1234567890ABCDEF_MatchesPublicTestVectors()
    {
        // Literal values computed independently from the public SplitMix64 reference
        // (https://prng.di.unimi.it/splitmix64.c) for the well-known seed 0x1234567890ABCDEF.
        ulong[] expected =
        [
            0x1C948E1575796814UL,
            0xAE9EF1AB67004BDBUL,
            0x7A2988D31F16E86EUL,
            0x7A5DAEA24EBA3BA7UL,
            0xBB83C0C2207AD3E6UL,
            0xE2DA71D9F0E79E32UL,
            0xF037B46F16A54449UL,
            0xAFD7E49C4512EE8CUL,
            0x25ADE43F8DCFFC85UL,
            0x0028CF578EC6BD94UL,
        ];

        ulong[] actual = ReferenceXoshiro256StarStar.SplitMix64Sequence(0x1234567890ABCDEFUL, 10);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SplitMix64_Seed0_MatchesPublicTestVectors()
    {
        // 0xE220A8397B1DCDAF is the widely cited first SplitMix64 output for seed 0.
        ulong[] expected =
        [
            0xE220A8397B1DCDAFUL,
            0x6E789E6AA1B965F4UL,
            0x06C45D188009454FUL,
            0xF88BB8A8724C81ECUL,
        ];

        ulong[] actual = ReferenceXoshiro256StarStar.SplitMix64Sequence(0UL, 4);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void DeterministicRandom_SameSeed_ProducesSameSequenceAcrossInstances(ulong seed)
    {
        var first = new DeterministicRandom(seed);
        var second = new DeterministicRandom(seed);

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }
}
