using System.Collections.Immutable;

namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §4.2's <c>ImmutableArray&lt;T&gt;</c> row ("default is treated as empty") is
/// explicitly a generator-level concern, not something <see cref="StableHashWriter"/> itself
/// special-cases (see SsalKit.StableHashing.Tests.EqualityConsistencyTests' remarks) -- the
/// generator must emit an <c>IsDefault</c> guard around the loop and pass <c>0</c> to
/// <see cref="StableHashWriter.AppendCount"/> for a default instance. This is verified here,
/// through the real generated code, per design doc §6 test 6.
/// </summary>
public class ImmutableArrayDefaultEqualsEmptyTests
{
    [Fact]
    public void DefaultImmutableArrayMember_ProducesSameHashAsEmptyImmutableArrayMember()
    {
        ComprehensiveContract withDefault = TestFixtures.BuildComprehensiveInstance() with { Immutable = default };
        ComprehensiveContract withEmpty = TestFixtures.BuildComprehensiveInstance() with { Immutable = ImmutableArray<int>.Empty };

        Assert.True(withDefault.Immutable.IsDefault);
        Assert.False(withEmpty.Immutable.IsDefault);
        Assert.True(withEmpty.Immutable.IsEmpty);

        Assert.Equal(withDefault.ComputeStableHash(), withEmpty.ComputeStableHash());
    }

    [Fact]
    public void DefaultImmutableArrayMember_StillDiffersFromNonEmptyImmutableArrayMember()
    {
        // Guards against an overzealous fix collapsing every ImmutableArray into the same hash
        // regardless of content.
        ComprehensiveContract withDefault = TestFixtures.BuildComprehensiveInstance() with { Immutable = default };
        ComprehensiveContract withOneElement = TestFixtures.BuildComprehensiveInstance() with { Immutable = [1] };

        Assert.NotEqual(withDefault.ComputeStableHash(), withOneElement.ComputeStableHash());
    }
}
