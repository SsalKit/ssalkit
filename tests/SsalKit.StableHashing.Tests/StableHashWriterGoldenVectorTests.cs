namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Pins the <see cref="StableHashWriter"/> v1 encoding contract (design doc §4.1/§4.4) with literal
/// expected <see cref="StableHash64"/> values (design doc §6 test 2) — the regression defense line
/// for the encoding: if any of these values ever changes, some rule in the permanent encoding
/// contract changed, which is exactly the kind of silent break the contract promises never happens
/// within a major version.
/// </summary>
/// <remarks>
/// Each pinned literal below was computed once from this library's own
/// <see cref="StableHashWriter"/> implementation. <see cref="ComprehensiveContract_MatchesIndependentManualByteSequence"/>
/// additionally builds a small contract's expected byte stream by hand (following §4.1/§4.4 rule by
/// rule) and hashes it with <c>System.IO.Hashing.XxHash64</c> — a real, independent implementation
/// — as a cross-check that is not just the writer agreeing with itself.
/// </remarks>
public class StableHashWriterGoldenVectorTests
{
    [Fact]
    public void ComprehensiveContract_AllSupportedTypes_MatchesPinnedLiteral()
    {
        StableHash64 hash = BuildComprehensiveContract();

        Assert.Equal(0x1E3291FD70D51445UL, hash.Value);
        Assert.Equal("1e3291fd70d51445", hash.ToString());
    }

    [Fact]
    public void HeaderOnlyContract_NoMembers_MatchesPinnedLiteral()
    {
        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendContractHeader("test.empty-contract", 1);
        StableHash64 hash = writer.Finish();

        Assert.Equal(0xFC947EA39FF77DBBUL, hash.Value);
    }

    [Fact]
    public void NumericBoundaryValues_MatchesPinnedLiteral()
    {
        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendContractHeader("test.numeric-boundaries", 1);

        writer.AppendMemberId(1);
        writer.AppendSByte(sbyte.MinValue);
        writer.AppendMemberId(2);
        writer.AppendSByte(sbyte.MaxValue);
        writer.AppendMemberId(3);
        writer.AppendInt64(long.MinValue);
        writer.AppendMemberId(4);
        writer.AppendInt64(long.MaxValue);
        writer.AppendMemberId(5);
        writer.AppendUInt64(ulong.MinValue);
        writer.AppendMemberId(6);
        writer.AppendUInt64(ulong.MaxValue);
        writer.AppendMemberId(7);
        writer.AppendInt128(Int128.MinValue);
        writer.AppendMemberId(8);
        writer.AppendInt128(Int128.MaxValue);
        writer.AppendMemberId(9);
        writer.AppendDecimal(decimal.MinValue);
        writer.AppendMemberId(10);
        writer.AppendDecimal(decimal.MaxValue);
        writer.AppendMemberId(11);
        writer.AppendDecimal(0m);

        StableHash64 hash = writer.Finish();

        Assert.Equal(0xC1BCC0802CC2DDEAUL, hash.Value);
    }

    /// <summary>
    /// Builds a small contract's expected byte stream entirely by hand, following the §4.1/§4.4
    /// encoding rules independently of <see cref="StableHashWriter"/>'s implementation, hashes it
    /// with the <c>System.IO.Hashing.XxHash64</c> oracle, and asserts it matches what the writer
    /// itself produces for the same logical contract. This is the golden-vector suite's one
    /// genuinely independent check (the other tests above pin values computed by the writer itself,
    /// so a bug shared between "compute" and "assert" would slip through them).
    /// </summary>
    [Fact]
    public void ComprehensiveContract_MatchesIndependentManualByteSequence()
    {
        // Contract "test.mini" v2, member 1 = int32(-5), member 2 = string("hi").
        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendContractHeader("test.mini", 2);
        writer.AppendMemberId(1);
        writer.AppendInt32(-5);
        writer.AppendMemberId(2);
        writer.AppendString("hi");
        StableHash64 writerHash = writer.Finish();

        byte[] manualBytes =
        [
            0x01, // format marker
            0x09, 0x00, 0x00, 0x00, // contract name UTF-8 byte count (9)
            0x74, 0x65, 0x73, 0x74, 0x2E, 0x6D, 0x69, 0x6E, 0x69, // "test.mini"
            0x02, 0x00, 0x00, 0x00, // version = 2
            0x01, 0x00, 0x00, 0x00, // member id = 1
            0xFB, 0xFF, 0xFF, 0xFF, // int32(-5), little-endian two's complement
            0x02, 0x00, 0x00, 0x00, // member id = 2
            0x02, 0x00, 0x00, 0x00, // string UTF-8 byte count (2)
            0x68, 0x69, // "hi"
        ];
        ulong oracleValue = System.IO.Hashing.XxHash64.HashToUInt64(manualBytes);

        Assert.Equal(oracleValue, writerHash.Value);
    }

    /// <summary>
    /// A representative contract exercising every v1-supported primitive and value type in one
    /// sequence, so a single pinned literal (<see cref="ComprehensiveContract_AllSupportedTypes_MatchesPinnedLiteral"/>)
    /// stands as a regression guard for the whole encoding table (design doc §4.4).
    /// </summary>
    internal static StableHash64 BuildComprehensiveContract()
    {
        StableHashWriter w = StableHashWriter.Create();
        w.AppendContractHeader("test.golden-vector", 1);

        w.AppendMemberId(1);
        w.AppendBoolean(true);

        w.AppendMemberId(2);
        w.AppendByte(200);

        w.AppendMemberId(3);
        w.AppendSByte(-100);

        w.AppendMemberId(4);
        w.AppendInt16(-12345);

        w.AppendMemberId(5);
        w.AppendUInt16(54321);

        w.AppendMemberId(6);
        w.AppendInt32(-123456789);

        w.AppendMemberId(7);
        w.AppendUInt32(3000000000);

        w.AppendMemberId(8);
        w.AppendInt64(-1234567890123456789L);

        w.AppendMemberId(9);
        w.AppendUInt64(12345678901234567890UL);

        w.AppendMemberId(10);
        w.AppendInt128(Int128.MinValue + 1);

        w.AppendMemberId(11);
        w.AppendUInt128(UInt128.MaxValue);

        w.AppendMemberId(12);
        w.AppendChar('한');

        w.AppendMemberId(13);
        w.AppendSingle(3.14f);

        w.AppendMemberId(14);
        w.AppendDouble(2.718281828);

        w.AppendMemberId(15);
        w.AppendDecimal(123.456m);

        w.AppendMemberId(16);
        w.AppendString("Hello, 세계!");

        w.AppendMemberId(17);
        w.AppendGuid(new Guid("12345678-1234-5678-1234-567812345678"));

        w.AppendMemberId(18);
        w.AppendDateOnly(new DateOnly(2026, 7, 30));

        w.AppendMemberId(19);
        w.AppendTimeOnly(new TimeOnly(13, 45, 30));

        w.AppendMemberId(20);
        w.AppendTimeSpan(TimeSpan.FromMinutes(90));

        w.AppendMemberId(21);
        w.AppendDateTimeOffset(new DateTimeOffset(2026, 7, 30, 4, 0, 0, TimeSpan.Zero));

        w.AppendMemberId(22);
        w.AppendNullMarker(true);
        w.AppendInt32(42);

        w.AppendMemberId(23);
        w.AppendNullMarker(false);

        w.AppendMemberId(24);
        w.AppendCount(2);
        w.AppendString("ab");
        w.AppendString("c");

        return w.Finish();
    }
}
