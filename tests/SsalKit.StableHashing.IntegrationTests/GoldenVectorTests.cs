namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §6 test 2 / §6 test 6 ("integration golden vector"): pins the real generated
/// <c>ComputeStableHash()</c> output for a representative contract instance as a literal, the
/// same regression-defense pattern
/// SsalKit.StableHashing.Tests.StableHashWriterGoldenVectorTests uses for the writer itself, but
/// here from the consumer's vantage point (through generated code, not hand-called
/// <see cref="StableHashWriter"/> methods). If this literal ever changes, either the encoding
/// contract (§4.1/§4.4) or the generator's adherence to it changed -- exactly the silent-break
/// class of bug this test exists to catch.
/// </summary>
/// <remarks>
/// Each literal below was computed once by running this project's own test suite and reading back
/// the produced value (see <see cref="GeneratedVsManualWriterTests"/> for the independent
/// hand-written cross-check that the same values are cross-verified against).
/// </remarks>
public class GoldenVectorTests
{
    [Fact]
    public void ComprehensiveContract_FixedInstance_MatchesPinnedLiteral()
    {
        ComprehensiveContract value = TestFixtures.BuildComprehensiveInstance();

        StableHash64 hash = value.ComputeStableHash();

        Assert.Equal(0xF61B09706711F1E1UL, hash.Value);
        Assert.Equal("f61b09706711f1e1", hash.ToString());
    }

    [Fact]
    public void Position_FixedInstance_MatchesPinnedLiteral()
    {
        var position = new Position { X = 11, Y = -22 };

        StableHash64 hash = position.ComputeStableHash();

        Assert.Equal(0xBB5D3FF140B08F33UL, hash.Value);
    }

    [Fact]
    public void PlayerName_FixedInstance_MatchesPinnedLiteral()
    {
        var playerName = new PlayerName { Value = "Ellin" };

        StableHash64 hash = playerName.ComputeStableHash();

        Assert.Equal(0x2E503F5E9DA48665UL, hash.Value);
    }
}
