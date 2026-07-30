namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §6 test 4 / §6 test 6: two instances differing in exactly one member must produce
/// different hashes through the real generated code, and identical instances must produce
/// identical hashes. SsalKit.StableHashing.Tests.DistinguishabilityTests already covers this at
/// the <see cref="StableHashWriter"/> layer directly; this class covers the same property end to
/// end through generated <c>ComputeStableHash()</c>.
/// </summary>
public class DistinguishabilityIntegrationTests
{
    [Fact]
    public void IdenticalInstances_ProduceTheSameHash()
    {
        ComprehensiveContract a = TestFixtures.BuildComprehensiveInstance();
        ComprehensiveContract b = TestFixtures.BuildComprehensiveInstance();

        Assert.Equal(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void SingleScalarMemberDifference_ProducesDifferentHash()
    {
        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance();
        ComprehensiveContract changed = baseline with { Int32 = baseline.Int32 + 1 };

        Assert.NotEqual(baseline.ComputeStableHash(), changed.ComputeStableHash());
    }

    [Fact]
    public void SingleEnumMemberDifference_ProducesDifferentHash()
    {
        ComprehensiveContract baseline = TestFixtures.BuildComprehensiveInstance() with { Rarity = Rarity.Common };
        ComprehensiveContract changed = baseline with { Rarity = Rarity.Rare };

        Assert.NotEqual(baseline.ComputeStableHash(), changed.ComputeStableHash());
    }

    [Fact]
    public void CollectionBoundary_AbCComma_DiffersFromA_BcComma_ThroughGeneratedStringCollectionEncoding()
    {
        // A generated-code analogue of SsalKit.StableHashing.Tests.DistinguishabilityTests'
        // AppendCount/AppendString boundary check: two Position lists (via a small local contract
        // built from Coordinate members) can't reuse the same string-collection member type this
        // project's ComprehensiveContract already tests numerically, so this exercises the
        // int-collection boundary instead: [1, 23] vs [12, 3] must not collide despite identical
        // concatenated digits.
        ComprehensiveContract first = TestFixtures.BuildComprehensiveInstance() with { List = [1, 23] };
        ComprehensiveContract second = TestFixtures.BuildComprehensiveInstance() with { List = [12, 3] };

        Assert.NotEqual(first.ComputeStableHash(), second.ComputeStableHash());
    }

    [Fact]
    public void DifferentElementCounts_ProducesDifferentHash()
    {
        ComprehensiveContract shorter = TestFixtures.BuildComprehensiveInstance() with { Array = [1, 2] };
        ComprehensiveContract longer = TestFixtures.BuildComprehensiveInstance() with { Array = [1, 2, 0] };

        Assert.NotEqual(shorter.ComputeStableHash(), longer.ComputeStableHash());
    }

    [Fact]
    public void DifferentContractTypes_WithStructurallySimilarMembers_ProduceDifferentHashes()
    {
        // Position (contract "integration.position") and Coordinate (contract
        // "integration.coordinate") both encode as exactly two int32 members with ids 1 and 2 --
        // only the contract-name header should keep them apart.
        var position = new Position { X = 3, Y = 4 };
        var coordinate = new Coordinate(3, 4);

        Assert.NotEqual(position.ComputeStableHash(), coordinate.ComputeStableHash());
    }
}
