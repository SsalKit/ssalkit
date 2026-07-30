using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SsalKit.StableHashing;

/// <summary>
/// Low-level, allocation-free API for building a <see cref="StableHash64"/> from a canonical byte
/// encoding of a value. This is the type SsalKit.StableHashing.Generator emits calls against; it
/// can also be used directly by hand for types the generator does not (yet) cover.
/// </summary>
/// <remarks>
/// <para>
/// <b>The encoding produced here is a permanent, versioned contract (v1).</b> Every rule below —
/// byte order, field widths, the floating-point and decimal normalization rules, the leading format
/// marker — is fixed forever for this type. Changing any of it would silently change every hash
/// this library has ever produced, which would be a form of data corruption for any consumer that
/// persisted a <see cref="StableHash64"/> value (as a database checksum, cache fingerprint, etc.).
/// If the encoding ever needs to evolve, it will ship as a new, separate API (e.g. a hypothetical
/// <c>StableHashWriterV2</c>) rather than by changing this one. The full v1 rule set:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="Create"/> writes a single leading format-marker byte (<c>0x01</c>) before anything
/// else, so that a future incompatible encoding scheme cannot accidentally share output with v1.
/// </description></item>
/// <item><description>All fixed-width integers are little-endian.</description></item>
/// <item><description><see langword="bool"/> is 1 byte (<c>0x00</c>/<c>0x01</c>).</description></item>
/// <item><description><see langword="char"/> is its UTF-16 code unit, as a little-endian <see langword="ushort"/>.</description></item>
/// <item><description>
/// <see cref="float"/>/<see cref="double"/>: negative zero is normalized to positive zero, and
/// every NaN bit pattern is normalized to a single canonical quiet NaN
/// (<c>0x7FC00000</c> / <c>0x7FF8000000000000</c>) before the bit pattern is written — this keeps
/// the encoding consistent with IEEE 754 equality, where <c>-0.0 == 0.0</c> and where NaN payloads
/// are not portable across platforms.
/// </description></item>
/// <item><description>
/// <see cref="decimal"/>: encoded as sign (1 byte) + scale (1 byte) + 96-bit mantissa
/// (12 bytes, little-endian), after normalizing away trailing zeros in the mantissa (dividing by 10
/// while the scale is positive and the mantissa is divisible by 10, using only integer arithmetic)
/// — this keeps <c>1.0m</c> and <c>1.00m</c>, which are <c>==</c> but have different underlying
/// bits, encoding identically.
/// </description></item>
/// <item><description>
/// <see cref="string"/>: a little-endian <see langword="int"/> UTF-8 byte count, then the UTF-8
/// bytes themselves. Malformed UTF-16 (e.g. an unpaired surrogate) falls back to
/// <see cref="Encoding.UTF8"/>'s default replacement-character behavior, which is itself
/// deterministic.
/// </description></item>
/// <item><description>
/// <see cref="Guid"/>: RFC 4122 big-endian byte order (i.e. the same order the string
/// representation implies), matching <c>Guid.TryWriteBytes(span, bigEndian: true, out _)</c>.
/// </description></item>
/// <item><description><see cref="DateOnly"/>: <see cref="DateOnly.DayNumber"/> as a little-endian <see langword="int"/>.</description></item>
/// <item><description><see cref="TimeOnly"/>/<see cref="TimeSpan"/>: <c>Ticks</c> as a little-endian <see langword="long"/>.</description></item>
/// <item><description>
/// <see cref="DateTimeOffset"/>: <em>only</em> <see cref="DateTimeOffset.UtcTicks"/>, as a
/// little-endian <see langword="long"/> — the offset is deliberately excluded, because
/// <see cref="DateTimeOffset"/> equality already compares the represented instant and ignores the
/// offset (<c>1pm+01:00 == noon+00:00</c>). If the offset itself is meaningful to a contract, store
/// it as a separate member.
/// </description></item>
/// <item><description>A null marker (used only for nullable members) is 1 byte: <c>0x00</c> absent, <c>0x01</c> present.</description></item>
/// </list>
/// <para>
/// <b>Not cryptographic.</b> See <see cref="StableHash64"/> remarks.
/// </para>
/// <para>
/// <b>Caller responsibility when used directly.</b> Unlike generated code — which the generator
/// guarantees calls these methods in a way consistent with the rules above — hand-written callers
/// are fully responsible for contract consistency: same fields, same order, same
/// <see cref="AppendMemberId"/>/<see cref="AppendNullMarker"/> placement, every time a given logical
/// value is encoded. Getting this wrong does not throw; it just silently produces a hash that is no
/// longer stable for that value.
/// </para>
/// <para>
/// <b>Not thread-safe, stack-only.</b> This is a <see langword="ref struct"/>: an instance can never
/// escape the stack (cannot be boxed, stored in a field of a non-ref-struct type, or captured by a
/// lambda/async method), so instances are inherently single-threaded and never shared.
/// </para>
/// <para>
/// <b>Implementation detail, not part of the contract:</b> individual <c>Append*</c> calls do not
/// necessarily reach <see cref="XxHash64"/> immediately. Small values are batched into an inline
/// staging buffer and flushed together, because the underlying XXH64 core processes input in
/// 32-byte lanes — feeding it one 4-16 byte value at a time (as a naive per-field design would) pays
/// the carry-buffer bookkeeping cost on nearly every call. Batching does not change a single byte of
/// the logical stream <see cref="XxHash64"/> ultimately sees; it only changes how many times
/// <see cref="XxHash64.Append"/> is called to deliver it. This is why the golden-vector tests are
/// the real proof of correctness for any change here: they pin the final hash, not the call pattern.
/// </para>
/// </remarks>
public ref struct StableHashWriter
{
    private const int StackAllocByteThreshold = 256;
    private const int CanonicalNaNSingleBits = 0x7FC00000;
    private const long CanonicalNaNDoubleBits = 0x7FF8000000000000L;

    /// <summary>
    /// Size of the inline staging buffer (see remarks). Chosen to equal
    /// <see cref="StackAllocByteThreshold"/>: large enough to batch a realistic contract's worth of
    /// scalar members (every fixed-width primitive this writer emits is at most 16 bytes) between
    /// flushes, an exact multiple of the 32-byte XXH64 lane so a full buffer flushes without leaving
    /// an odd remainder for the hasher's own carry buffer to absorb, and small enough to stay a
    /// trivial stack allocation.
    /// </summary>
    private const int StagingBufferSize = 256;

    [InlineArray(StagingBufferSize)]
    private struct StagingBuffer
    {
        private byte _element0;
    }

    private XxHash64 _hasher;
    private StagingBuffer _staging;
    private int _stagingLength;

    private StableHashWriter(XxHash64 hasher) => _hasher = hasher;

    /// <summary>
    /// Creates a new writer. Writes the leading v1 format-marker byte (<c>0x01</c>) immediately.
    /// </summary>
    /// <returns>A new, ready-to-use writer.</returns>
    public static StableHashWriter Create()
    {
        var writer = new StableHashWriter(XxHash64.Create());
        ReadOnlySpan<byte> marker = [0x01];
        writer.WriteToStaging(marker);
        return writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> StagingSpan() => MemoryMarshal.CreateSpan(ref _staging[0], StagingBufferSize);

    /// <summary>
    /// Copies <paramref name="bytes"/> into the staging buffer, flushing first if it does not
    /// currently fit in the free space. This is the single choke point every raw <c>Append*</c>
    /// method routes through, so the byte order the hasher ultimately observes is identical to
    /// calling <see cref="XxHash64.Append"/> directly on every value in call order — batching only
    /// changes when bytes cross into the hasher, never their order or content.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteToStaging(scoped ReadOnlySpan<byte> bytes)
    {
        // WriteToStaging is only ever called with fixed-width primitives (at most 16 bytes, for
        // Guid/Int128/UInt128 -- the widest values this writer emits), never with string/collection
        // bodies (those go through AppendString's own staging-or-direct split). A single value
        // that cannot possibly fit the staging buffer at all is therefore unreachable from any
        // current call site; per repository policy this is asserted rather than given an untestable
        // "handle it anyway" branch (see AppendString for the actual over-threshold code path).
        Debug.Assert(bytes.Length < StagingBufferSize, "WriteToStaging is only used for small, fixed-width primitives.");

        if (_stagingLength + bytes.Length > StagingBufferSize)
        {
            FlushStaging();
        }

        bytes.CopyTo(StagingSpan().Slice(_stagingLength));
        _stagingLength += bytes.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FlushStaging()
    {
        if (_stagingLength > 0)
        {
            _hasher.Append(StagingSpan()[.._stagingLength]);
            _stagingLength = 0;
        }
    }

    /// <summary>
    /// Appends a contract's header: the contract name (as <see cref="AppendString"/>) followed by
    /// its version as a little-endian <see langword="int"/>. Called once per contract, including
    /// once for every nested contract value (nested values are encoded recursively, header and all
    /// — see <see cref="StableHash64"/> remarks on nested contracts propagating changes correctly).
    /// </summary>
    /// <param name="contractName">The contract's stable name (see <see cref="StableHashContractAttribute.Name"/>).</param>
    /// <param name="version">The contract's version (see <see cref="StableHashContractAttribute.Version"/>).</param>
    public void AppendContractHeader(string contractName, int version)
    {
        AppendString(contractName);
        AppendInt32(version);
    }

    /// <summary>
    /// Appends a member identifier (see <see cref="StableHashMemberAttribute.Id"/>), encoded
    /// identically to <see cref="AppendInt32"/>. Generated code calls this immediately before
    /// appending the member's value.
    /// </summary>
    /// <param name="memberId">The member's stable identifier.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendMemberId(int memberId) => AppendInt32(memberId);

    /// <summary>
    /// Appends a 1-byte null marker for a nullable member: <c>0x01</c> when <paramref name="hasValue"/>
    /// is <see langword="true"/> (the member's value follows immediately after), <c>0x00</c> when
    /// <see langword="false"/> (no value follows). Non-nullable members never call this.
    /// </summary>
    /// <param name="hasValue">Whether the nullable member currently holds a value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendNullMarker(bool hasValue)
    {
        ReadOnlySpan<byte> marker = [hasValue ? (byte)0x01 : (byte)0x00];
        WriteToStaging(marker);
    }

    /// <summary>
    /// Appends a collection element count, encoded identically to <see cref="AppendInt32"/>. Written
    /// before a collection's elements so that, for example, <c>["ab", "c"]</c> and <c>["a", "bc"]</c>
    /// cannot be confused with each other purely from their concatenated string bytes.
    /// </summary>
    /// <param name="count">The number of elements about to be appended.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendCount(int count) => AppendInt32(count);

    /// <summary>Appends a <see langword="bool"/> as 1 byte (<c>0x00</c>/<c>0x01</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendBoolean(bool value)
    {
        ReadOnlySpan<byte> b = [value ? (byte)0x01 : (byte)0x00];
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="char"/> as its UTF-16 code unit, little-endian <see langword="ushort"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendChar(char value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends an <see langword="sbyte"/> as its single raw byte.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendSByte(sbyte value)
    {
        ReadOnlySpan<byte> b = [unchecked((byte)value)];
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="byte"/> as its single raw byte.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendByte(byte value)
    {
        ReadOnlySpan<byte> b = [value];
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="short"/>, little-endian, fixed 2 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendInt16(short value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="ushort"/>, little-endian, fixed 2 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendUInt16(ushort value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends an <see langword="int"/>, little-endian, fixed 4 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendInt32(int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="uint"/>, little-endian, fixed 4 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendUInt32(uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="long"/>, little-endian, fixed 8 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendInt64(long value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends a <see langword="ulong"/>, little-endian, fixed 8 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendUInt64(ulong value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends an <see cref="Int128"/>, little-endian, fixed 16 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendInt128(Int128 value)
    {
        Span<byte> b = stackalloc byte[16];
        BinaryPrimitives.WriteInt128LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>Appends a <see cref="UInt128"/>, little-endian, fixed 16 bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendUInt128(UInt128 value)
    {
        Span<byte> b = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(b, value);
        WriteToStaging(b);
    }

    /// <summary>
    /// Appends a <see langword="float"/>: negative zero is normalized to positive zero and any NaN
    /// is normalized to the canonical quiet NaN bit pattern (<c>0x7FC00000</c>), then the resulting
    /// bit pattern is written little-endian as 4 bytes. See <see cref="StableHashWriter"/> remarks
    /// for why.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendSingle(float value)
    {
        if (float.IsNaN(value))
        {
            value = BitConverter.Int32BitsToSingle(CanonicalNaNSingleBits);
        }
        else if (value == 0f)
        {
            value = 0f;
        }

        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, BitConverter.SingleToInt32Bits(value));
        WriteToStaging(b);
    }

    /// <summary>
    /// Appends a <see langword="double"/>: negative zero is normalized to positive zero and any NaN
    /// is normalized to the canonical quiet NaN bit pattern (<c>0x7FF8000000000000</c>), then the
    /// resulting bit pattern is written little-endian as 8 bytes. See <see cref="StableHashWriter"/>
    /// remarks for why.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendDouble(double value)
    {
        if (double.IsNaN(value))
        {
            value = BitConverter.Int64BitsToDouble(CanonicalNaNDoubleBits);
        }
        else if (value == 0d)
        {
            value = 0d;
        }

        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, BitConverter.DoubleToInt64Bits(value));
        WriteToStaging(b);
    }

    /// <summary>
    /// Appends a <see langword="decimal"/> as sign (1 byte: <c>0x00</c>/<c>0x01</c>) + scale
    /// (1 byte) + 96-bit mantissa (12 bytes, little-endian), after normalizing away trailing zeros
    /// in the mantissa using only integer arithmetic (dividing by 10 while the scale is positive and
    /// the mantissa is divisible by 10 — at most 28 iterations). See <see cref="StableHashWriter"/>
    /// remarks for why this normalization is necessary for equality consistency. Negative zero and
    /// every other all-zero representation (<c>0m</c>, <c>-0.0m</c>, <c>0.00m</c>, ...) normalize to
    /// a single canonical zero encoding (sign <c>0x00</c>, scale <c>0</c>, mantissa <c>0</c>) — decimal
    /// equality (<c>-0.0m == 0.0m</c>) does not distinguish the sign of zero, so the encoding must not
    /// either.
    /// </summary>
    public void AppendDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);

        uint lo = unchecked((uint)bits[0]);
        uint mid = unchecked((uint)bits[1]);
        uint hi = unchecked((uint)bits[2]);
        uint flags = unchecked((uint)bits[3]);

        byte sign = (flags & 0x8000_0000u) != 0 ? (byte)0x01 : (byte)0x00;
        byte scale = (byte)((flags >> 16) & 0xFF);

        UInt128 mantissa = ((UInt128)hi << 64) | ((UInt128)mid << 32) | lo;
        while (scale > 0 && mantissa % 10 == 0)
        {
            mantissa /= 10;
            scale--;
        }

        if (mantissa == 0)
        {
            // The normalization loop above already drives scale to 0 whenever mantissa is 0 (0 is
            // divisible by 10 at every step), but it cannot fix the sign bit -- decimal.GetBits
            // preserves a negative-zero sign flag even though decimal equality treats -0.0m and
            // 0.0m as equal. Force a single canonical zero encoding here.
            sign = 0x00;
        }

        Span<byte> encoded = stackalloc byte[14];
        encoded[0] = sign;
        encoded[1] = scale;

        Span<byte> mantissaBytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(mantissaBytes, mantissa);
        mantissaBytes[..12].CopyTo(encoded[2..]);

        WriteToStaging(encoded);
    }

    /// <summary>
    /// Appends a <see langword="string"/> as a little-endian <see langword="int"/> UTF-8 byte count
    /// followed by the UTF-8 bytes themselves. Encoding uses <see cref="Encoding.UTF8"/>'s default
    /// replacement-character fallback for malformed UTF-16 input, which is deterministic. Allocates
    /// nothing on the managed heap when the UTF-8 byte count is at most 256; longer strings rent a
    /// buffer from <see cref="ArrayPool{T}.Shared"/> instead of allocating.
    /// </summary>
    /// <param name="value">The string to append. Must not be <see langword="null"/>.</param>
    public void AppendString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int byteCount = Encoding.UTF8.GetByteCount(value);
        AppendInt32(byteCount);

        int freeInStaging = StagingBufferSize - _stagingLength;
        if (byteCount <= freeInStaging)
        {
            // Fits in whatever staging space is currently free -- encode straight into it, no
            // temporary buffer or extra copy needed. Byte order is unaffected: this is exactly the
            // bytes AppendString would otherwise hand to WriteToStaging, just written directly at
            // their final staging offset instead of being copied there from a stackalloc buffer.
            Span<byte> destination = StagingSpan().Slice(_stagingLength, byteCount);
            int writtenToStaging = Encoding.UTF8.GetBytes(value, destination);
            Debug.Assert(writtenToStaging == byteCount, "GetByteCount and GetBytes disagreed on UTF-8 length.");
            _stagingLength += writtenToStaging;
            return;
        }

        // Doesn't fit in what's currently free in staging. Flush first so the length prefix (and
        // anything staged before it) reaches the hasher in order, then encode the body via the
        // existing stackalloc/ArrayPool path and feed the hasher directly -- bypassing staging for
        // this call rather than trying to re-fit the body into the now-empty buffer.
        FlushStaging();

        if (byteCount <= StackAllocByteThreshold)
        {
            Span<byte> buffer = stackalloc byte[StackAllocByteThreshold];
            int written = Encoding.UTF8.GetBytes(value, buffer);
            Debug.Assert(written == byteCount, "GetByteCount and GetBytes disagreed on UTF-8 length.");
            _hasher.Append(buffer[..written]);
        }
        else
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                int written = Encoding.UTF8.GetBytes(value, rented);
                _hasher.Append(rented.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Appends a <see cref="Guid"/> in RFC 4122 big-endian byte order (the order its string
    /// representation implies), via <c>Guid.TryWriteBytes(span, bigEndian: true, out _)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendGuid(Guid value)
    {
        Span<byte> b = stackalloc byte[16];
        value.TryWriteBytes(b, bigEndian: true, out _);
        WriteToStaging(b);
    }

    /// <summary>Appends a <see cref="DateOnly"/> as its <see cref="DateOnly.DayNumber"/>, little-endian <see langword="int"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendDateOnly(DateOnly value) => AppendInt32(value.DayNumber);

    /// <summary>Appends a <see cref="TimeOnly"/> as its <c>Ticks</c>, little-endian <see langword="long"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendTimeOnly(TimeOnly value) => AppendInt64(value.Ticks);

    /// <summary>Appends a <see cref="TimeSpan"/> as its <c>Ticks</c>, little-endian <see langword="long"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendTimeSpan(TimeSpan value) => AppendInt64(value.Ticks);

    /// <summary>
    /// Appends a <see cref="DateTimeOffset"/> as <em>only</em> its <see cref="DateTimeOffset.UtcTicks"/>,
    /// little-endian <see langword="long"/> — the offset itself is deliberately not encoded. See
    /// <see cref="StableHashWriter"/> remarks for why.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendDateTimeOffset(DateTimeOffset value) => AppendInt64(value.UtcTicks);

    /// <summary>
    /// Finalizes the writer and returns the resulting <see cref="StableHash64"/>. Flushes any bytes
    /// still sitting in the internal staging buffer before digesting. The writer must not be used
    /// again after calling this.
    /// </summary>
    /// <returns>The computed hash.</returns>
    public StableHash64 Finish()
    {
        FlushStaging();
        return new(_hasher.Digest());
    }
}
