namespace SsalKit.Randomness.Tests;

public class ForkTests
{
    [Fact]
    public void Fork_SameParentState_ProducesSameChildSequence()
    {
        var parentA = new DeterministicRandom(2026UL);
        var parentB = new DeterministicRandom(2026UL);

        DeterministicRandom childA = parentA.Fork();
        DeterministicRandom childB = parentB.Fork();

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(childA.NextUInt64(), childB.NextUInt64());
        }
    }

    [Fact]
    public void Fork_AdvancesParentByExactlyOneStep()
    {
        var reference = new DeterministicRandom(2026UL);
        ulong expectedForkedSeed = reference.NextUInt64();

        var parent = new DeterministicRandom(2026UL);
        parent.Fork();

        // After Fork(), the parent's state must be exactly what it would have been after a
        // single NextUInt64() call: the rest of the parent's sequence continues unbroken.
        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(reference.NextUInt64(), parent.NextUInt64());
        }

        // Sanity: the value consumed to seed the fork really was the parent's "next" value.
        var freshReference = new DeterministicRandom(2026UL);
        Assert.Equal(expectedForkedSeed, freshReference.NextUInt64());
    }

    [Fact]
    public void Fork_ContractMatchesSeedConstructorOfConsumedValue()
    {
        // v1 contract: Fork() == new DeterministicRandom(this.NextUInt64())
        var parentForFork = new DeterministicRandom(31415UL);
        var parentForManualSeed = new DeterministicRandom(31415UL);

        DeterministicRandom child = parentForFork.Fork();
        ulong manuallyExtractedSeed = parentForManualSeed.NextUInt64();
        var expectedChild = new DeterministicRandom(manuallyExtractedSeed);

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(expectedChild.NextUInt64(), child.NextUInt64());
        }
    }

    [Fact]
    public void Fork_ChildIsIndependentFromParentAfterForking()
    {
        var parent = new DeterministicRandom(7UL);
        DeterministicRandom child = parent.Fork();

        // Advancing the child must not affect the parent's subsequent outputs.
        ulong[] parentContinuation = new ulong[8];
        var parentReference = new DeterministicRandom(7UL);
        parentReference.NextUInt64(); // account for the value Fork() consumed
        for (int i = 0; i < parentContinuation.Length; i++)
        {
            parentContinuation[i] = parentReference.NextUInt64();
        }

        for (int i = 0; i < 100; i++)
        {
            child.NextUInt64();
        }

        for (int i = 0; i < parentContinuation.Length; i++)
        {
            Assert.Equal(parentContinuation[i], parent.NextUInt64());
        }
    }

    [Fact]
    public void Fork_RepeatedForksFromSameParentProduceDifferentChildren()
    {
        var parent = new DeterministicRandom(99UL);

        DeterministicRandom childOne = parent.Fork();
        DeterministicRandom childTwo = parent.Fork();

        Assert.NotEqual(childOne.NextUInt64(), childTwo.NextUInt64());
    }
}
