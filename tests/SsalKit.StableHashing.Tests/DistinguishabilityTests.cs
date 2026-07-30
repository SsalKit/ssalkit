namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Verifies that the structural markers the encoding relies on — member ids, contract name,
/// contract version, the null marker, and collection element counts — actually distinguish inputs
/// that would otherwise collide (design doc §6 test 4). This is what justifies those markers'
/// existence: without them, ["ab", "c"] and ["a", "bc"] would encode to the same byte stream once
/// concatenated.
/// </summary>
public class DistinguishabilityTests
{
    [Fact]
    public void AppendBoolean_TrueVsFalse_ProducesDifferentHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendBoolean(true);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendBoolean(false);
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void DifferentMemberId_SameValue_ProducesDifferentHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendMemberId(1);
        w1.AppendInt32(42);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendMemberId(2);
        w2.AppendInt32(42);
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void DifferentContractName_ProducesDifferentHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendContractHeader("contract.a", 1);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendContractHeader("contract.b", 1);
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void DifferentContractVersion_ProducesDifferentHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendContractHeader("contract.a", 1);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendContractHeader("contract.a", 2);
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void NullMarker_PresentVsAbsent_ProducesDifferentHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendNullMarker(true);
        w1.AppendInt32(0);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendNullMarker(false);
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void NullMarker_AbsentVsNoMarkerAtAll_ProducesDifferentHash()
    {
        // A nullable member with no value (marker written) must not collide with simply omitting
        // the member entirely (no marker, no value) -- the marker byte itself must show up.
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendNullMarker(false);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void CollectionBoundary_AbCComma_DiffersFromA_BcComma()
    {
        // ["ab", "c"] vs ["a", "bc"]: same concatenated character stream ("abc"), but AppendCount +
        // per-element AppendString length prefixes must keep them apart.
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendCount(2);
        w1.AppendString("ab");
        w1.AppendString("c");
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendCount(2);
        w2.AppendString("a");
        w2.AppendString("bc");
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void CollectionCount_DifferentElementCounts_ProducesDifferentHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendCount(1);
        w1.AppendString("ab");
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendCount(2);
        w2.AppendString("a");
        w2.AppendString("b");
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void StringLengthPrefix_PreventsConcatenationCollision()
    {
        // "ab"+"c" vs "a"+"bc" without any count/id markers at all -- still distinguished purely by
        // AppendString's own int32 length prefix per call.
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendString("ab");
        w1.AppendString("c");
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendString("a");
        w2.AppendString("bc");
        ulong h2 = w2.Finish().Value;

        Assert.NotEqual(h1, h2);
    }
}
