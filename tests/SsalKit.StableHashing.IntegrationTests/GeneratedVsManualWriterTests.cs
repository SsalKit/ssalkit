namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §6 test 6 ("integration test... cross-checked against the runtime golden vector"):
/// asserts that the real generated <c>ComputeStableHash()</c> extension for
/// <see cref="ComprehensiveContract"/> -- produced by SsalKit.StableHashing.Generator running as
/// an analyzer against this project, exactly as a NuGet consumer would experience it -- agrees
/// byte-for-byte (in hash output) with an independent, hand-written encoding of the same logical
/// data built directly against <see cref="StableHashWriter"/> (<see cref="TestFixtures.EncodeManually"/>).
/// This is what proves the generator actually implements the §4.1/§4.4 encoding order/rules
/// end-to-end, not just that its snapshot tests look plausible.
/// </summary>
public class GeneratedVsManualWriterTests
{
    [Fact]
    public void ComprehensiveContract_GeneratedComputeStableHash_MatchesHandWrittenEncoding()
    {
        ComprehensiveContract value = TestFixtures.BuildComprehensiveInstance();

        StableHash64 generated = value.ComputeStableHash();
        StableHash64 manual = TestFixtures.EncodeManually(value);

        Assert.Equal(manual, generated);
        Assert.Equal(manual.Value, generated.Value);
    }

    [Fact]
    public void ComprehensiveContract_DefaultValues_GeneratedMatchesHandWrittenEncoding()
    {
        // A second, structurally different instance (default/empty/absent everywhere a choice
        // exists) so the cross-check is not accidentally only valid for one specific data shape.
        var value = new ComprehensiveContract
        {
            String = "",
            NullableInt = null,
            NullableString = null,
            Array = [],
            List = [],
            ReadOnlyList = [],
            Immutable = default,
            NestedPosition = default,
            Rarity = Rarity.Common,
        };

        StableHash64 generated = value.ComputeStableHash();
        StableHash64 manual = TestFixtures.EncodeManually(value);

        Assert.Equal(manual, generated);
    }

    [Fact]
    public void NestedPositionContract_GeneratedComputeStableHash_MatchesHandWrittenEncoding()
    {
        var position = new Position { X = 7, Y = -3 };

        StableHash64 generated = position.ComputeStableHash();

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendContractHeader("integration.position", 1);
        writer.AppendMemberId(1);
        writer.AppendInt32(position.X);
        writer.AppendMemberId(2);
        writer.AppendInt32(position.Y);
        StableHash64 manual = writer.Finish();

        Assert.Equal(manual, generated);
    }
}
