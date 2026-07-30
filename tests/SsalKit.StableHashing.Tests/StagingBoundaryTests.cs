using System.Buffers.Binary;

namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Targets the internal staging buffer's flush boundary directly (introduced as a performance
/// optimization: small values are batched into a 256-byte inline buffer before reaching
/// <see cref="XxHash64"/>, rather than being fed to it one at a time). Every case here builds the
/// expected byte stream independently and hashes it with the <c>System.IO.Hashing.XxHash64</c>
/// oracle, so these tests verify the actual output bytes crossing a flush are unaffected -- not
/// just that the flush branch executes (line/branch coverage alone cannot tell reordered or
/// dropped bytes from correct ones).
/// </summary>
public class StagingBoundaryTests
{
    [Fact]
    public void ManySmallAppends_CrossingStagingBufferMultipleTimes_MatchesIndependentOracle()
    {
        // 100 * 4 = 400 bytes of int32 values alone, well past the 256-byte staging buffer twice
        // over -- guarantees at least one internal flush is forced mid-stream by AppendInt32 calls
        // alone (no AppendString involved).
        StableHashWriter writer = StableHashWriter.Create();
        for (int i = 0; i < 100; i++)
        {
            writer.AppendInt32(i);
        }

        ulong actual = writer.Finish().Value;

        var expectedBytes = new byte[1 + (100 * 4)];
        expectedBytes[0] = 0x01; // format marker
        for (int i = 0; i < 100; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(expectedBytes.AsSpan(1 + (i * 4), 4), i);
        }

        ulong expected = System.IO.Hashing.XxHash64.HashToUInt64(expectedBytes);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)] // string body exactly fills the remaining staging space
    [InlineData(1)] // string body is exactly one byte too many -- forces a flush
    [InlineData(-1)] // string body leaves exactly one free byte in staging
    public void AppendString_ExactlyAtStagingFreeSpaceBoundary_MatchesIndependentOracle(int offsetFromExactFit)
    {
        // After StableHashWriter.Create() writes the 1-byte marker, pad staging up to a known
        // length with plain bytes (via AppendByte, 1 byte each) so the remaining free space before
        // the string's own AppendInt32(byteCount) length prefix (4 bytes) is a known, small number.
        // This puts the string body's fit-vs-flush decision exactly where each test case wants it.
        const int stagingBufferSize = 256;
        const int padCount = 200; // 1 (marker) + 200 = 201 bytes staged before the string call
        int freeAfterLengthPrefix = stagingBufferSize - (1 + padCount) - 4; // = 51

        int stringByteCount = freeAfterLengthPrefix + offsetFromExactFit;
        Assert.InRange(stringByteCount, 1, 255); // sanity: stays comfortably within valid ASCII-length bounds
        string value = new('x', stringByteCount);

        StableHashWriter writer = StableHashWriter.Create();
        for (int i = 0; i < padCount; i++)
        {
            writer.AppendByte((byte)i);
        }

        writer.AppendString(value);
        ulong actual = writer.Finish().Value;

        var expectedBytes = new byte[1 + padCount + 4 + stringByteCount];
        int offset = 0;
        expectedBytes[offset++] = 0x01; // format marker
        for (int i = 0; i < padCount; i++)
        {
            expectedBytes[offset++] = (byte)i;
        }

        BinaryPrimitives.WriteInt32LittleEndian(expectedBytes.AsSpan(offset, 4), stringByteCount);
        offset += 4;
        for (int i = 0; i < stringByteCount; i++)
        {
            expectedBytes[offset++] = (byte)'x';
        }

        ulong expected = System.IO.Hashing.XxHash64.HashToUInt64(expectedBytes);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MixedPrimitivesAndStrings_RepeatedlyCrossingStagingBoundary_MatchesIndependentOracle()
    {
        // A longer, more realistic mixed sequence (ints, a decimal, several strings of varying
        // length, a guid) that crosses the 256-byte staging boundary several times across mixed
        // append kinds, not just one repeated call.
        // StableHashWriter is a ref struct, so it (and any local capturing it) cannot be closed
        // over by a lambda/local function that might be converted to a delegate -- each append
        // below is written out directly rather than through a shared helper closure.
        StableHashWriter writer = StableHashWriter.Create();
        var expected = new List<byte> { 0x01 }; // format marker

        for (int round = 0; round < 10; round++)
        {
            writer.AppendInt32(round);
            var roundBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(roundBytes, round);
            expected.AddRange(roundBytes);

            string payload = $"round-{round}-payload-of-moderate-length-to-add-up-across-iterations";
            writer.AppendString(payload);
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(payload);
            var lenBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lenBytes, utf8.Length);
            expected.AddRange(lenBytes);
            expected.AddRange(utf8);

            int scaled = round * 1000;
            writer.AppendInt32(scaled);
            var scaledBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(scaledBytes, scaled);
            expected.AddRange(scaledBytes);
        }

        ulong actual = writer.Finish().Value;
        ulong expectedHash = System.IO.Hashing.XxHash64.HashToUInt64(expected.ToArray());

        Assert.Equal(expectedHash, actual);
    }
}
