using System.Text;

namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Boundary-condition coverage (design doc §6 "boundary" bullet): the empty string, strings that
/// land exactly on and just past the <see cref="StableHashWriter.AppendString"/> stackalloc
/// threshold (256 UTF-8 bytes, including multi-byte Korean input so the boundary is exercised in
/// character-count terms too), <see langword="decimal"/> zero/negative/<see cref="decimal.MaxValue"/>,
/// and <see cref="Guid"/> round-trip representation.
/// </summary>
public class BoundaryTests
{
    [Fact]
    public void AppendString_EmptyString_ProducesDeterministicHash()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendString(string.Empty);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendString(string.Empty);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendString_EmptyString_DiffersFromNonEmptyOfCount0Bytes()
    {
        // Sanity: the empty string still writes its 0-length prefix and hashes differently from
        // "nothing appended at all" (see DistinguishabilityTests for the general marker-presence
        // principle).
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendString(string.Empty);
        ulong withEmptyString = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        ulong withNothing = w2.Finish().Value;

        Assert.NotEqual(withEmptyString, withNothing);
    }

    public static TheoryData<string, int> StackallocBoundaryStrings()
    {
        var data = new TheoryData<string, int>();

        string ascii256 = new('a', 256);
        string ascii257 = new('a', 257);
        Assert.Equal(256, Encoding.UTF8.GetByteCount(ascii256));
        Assert.Equal(257, Encoding.UTF8.GetByteCount(ascii257));
        data.Add(ascii256, 256);
        data.Add(ascii257, 257);

        // Korean syllables are 3 UTF-8 bytes each.
        string korean255 = new('한', 85); // 85 * 3 = 255 bytes, just under the threshold
        string korean258 = new('한', 86); // 86 * 3 = 258 bytes, just over the threshold
        Assert.Equal(255, Encoding.UTF8.GetByteCount(korean255));
        Assert.Equal(258, Encoding.UTF8.GetByteCount(korean258));
        data.Add(korean255, 255);
        data.Add(korean258, 258);

        // Mixed Korean + ASCII landing exactly on the threshold and one past it.
        string koreanAscii256 = new string('한', 85) + "a"; // 255 + 1 = 256 bytes, exactly at threshold
        string koreanAscii257 = new string('한', 85) + "ab"; // 255 + 2 = 257 bytes, just past threshold
        Assert.Equal(256, Encoding.UTF8.GetByteCount(koreanAscii256));
        Assert.Equal(257, Encoding.UTF8.GetByteCount(koreanAscii257));
        data.Add(koreanAscii256, 256);
        data.Add(koreanAscii257, 257);

        return data;
    }

    [Theory]
    [MemberData(nameof(StackallocBoundaryStrings))]
    public void AppendString_AtOrAroundStackallocThreshold_MatchesIndependentOracleEncoding(string value, int expectedByteCount)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);
        Assert.Equal(expectedByteCount, utf8Bytes.Length);

        // 1-byte format marker (written by StableHashWriter.Create) + int32 LE length prefix + bytes.
        byte[] manualBytes = new byte[1 + 4 + utf8Bytes.Length];
        manualBytes[0] = 0x01;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(manualBytes.AsSpan(1, 4), utf8Bytes.Length);
        utf8Bytes.CopyTo(manualBytes, 5);
        ulong expected = System.IO.Hashing.XxHash64.HashToUInt64(manualBytes);

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendString(value);
        ulong actual = writer.Finish().Value;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(StackallocBoundaryStrings))]
    public void AppendString_AtOrAroundStackallocThreshold_IsDeterministicAcrossCalls(string value, int expectedByteCount)
    {
        _ = expectedByteCount;

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendString(value);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendString(value);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendDecimal_Zero_IsDeterministic()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDecimal(0m);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDecimal(0m);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendDecimal_NegativeValue_DiffersFromPositiveCounterpart()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDecimal(-123.45m);
        ulong negative = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDecimal(123.45m);
        ulong positive = w2.Finish().Value;

        Assert.NotEqual(negative, positive);
    }

    [Fact]
    public void AppendDecimal_MaxValue_DoesNotThrowAndIsDeterministic()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDecimal(decimal.MaxValue);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDecimal(decimal.MaxValue);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendDecimal_MinValue_DiffersFromMaxValue()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDecimal(decimal.MinValue);
        ulong min = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDecimal(decimal.MaxValue);
        ulong max = w2.Finish().Value;

        Assert.NotEqual(min, max);
    }

    [Fact]
    public void AppendGuid_MatchesRfc4122BigEndianStringRepresentation()
    {
        var guid = new Guid("12345678-9abc-def0-1122-334455667788");

        // The RFC 4122 big-endian byte order is exactly the hex digits of the "D"-format string
        // with dashes removed, read left to right -- an independent hand-derivation of the expected
        // wire bytes that does not depend on Guid.TryWriteBytes at all.
        byte[] expectedBytes = Convert.FromHexString("123456789abcdef01122334455667788");
        Assert.Equal(16, expectedBytes.Length);

        Span<byte> viaWriterBuffer = stackalloc byte[16];
        bool ok = guid.TryWriteBytes(viaWriterBuffer, bigEndian: true, out _);
        Assert.True(ok);
        Assert.Equal(expectedBytes, viaWriterBuffer.ToArray());

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendGuid(guid);
        ulong expectedHash = w1.Finish().Value;

        ReadOnlySpan<byte> withMarker = [0x01, .. expectedBytes];
        ulong oracleHash = System.IO.Hashing.XxHash64.HashToUInt64(withMarker);

        Assert.Equal(oracleHash, expectedHash);
    }

    [Fact]
    public void AppendGuid_SameGuidParsedTwoWays_ProducesSameHash()
    {
        var fromDashed = Guid.Parse("12345678-1234-5678-1234-567812345678");
        var fromBytes = new Guid(fromDashed.ToByteArray());
        Assert.Equal(fromDashed, fromBytes);

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendGuid(fromDashed);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendGuid(fromBytes);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendGuid_Empty_IsDeterministicAndDiffersFromNonEmpty()
    {
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendGuid(Guid.Empty);
        ulong empty = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendGuid(new Guid("11111111-2222-3333-4444-555555555555"));
        ulong nonEmpty = w2.Finish().Value;

        StableHashWriter w3 = StableHashWriter.Create();
        w3.AppendGuid(Guid.Empty);
        ulong emptyAgain = w3.Finish().Value;

        Assert.Equal(empty, emptyAgain);
        Assert.NotEqual(empty, nonEmpty);
    }
}
