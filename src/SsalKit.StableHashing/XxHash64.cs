using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SsalKit.StableHashing;

/// <summary>
/// Streaming (incremental), seed-0 implementation of XXH64, the 64-bit member of the xxHash
/// family. This is a direct, from-scratch port of the public XXH64 reference algorithm
/// (https://github.com/Cyan4973/xxHash — specification frozen since 2014), kept internal to this
/// package so <c>SsalKit.StableHashing</c> has zero external runtime dependencies.
/// </summary>
/// <remarks>
/// The choice of XXH64 (over, say, the faster but more complex XXH3) and the fixed seed of 0 are
/// both part of <see cref="StableHashWriter"/>'s permanent v1 encoding contract — see that type's
/// remarks. State is 4 <see cref="ulong"/> accumulators, a running total length, and a 32-byte
/// carry buffer for input that has not yet formed a complete lane block; all held inline (no heap
/// allocation).
/// </remarks>
internal struct XxHash64
{
    private const ulong Prime1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime3 = 0x165667B19E3779F9UL;
    private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime5 = 0x27D4EB2F165667C5UL;

    private const int BufferSize = 32;

    [InlineArray(BufferSize)]
    private struct CarryBuffer
    {
        private byte _element0;
    }

    private ulong _v1;
    private ulong _v2;
    private ulong _v3;
    private ulong _v4;
    private ulong _totalLength;
    private int _carryLength;
    // Written only through the writable span over its elements (CarrySpan) — ReSharper's
    // analysis does not recognize InlineArray element-ref writes as assignments (Roslyn does,
    // hence no CS0649 from the actual build).
    // ReSharper disable once UnassignedField.Compiler
    private CarryBuffer _carry;

    /// <summary>Creates a new streaming hasher state, seed fixed at 0.</summary>
    public static XxHash64 Create()
    {
        XxHash64 hasher = default;
        hasher._v1 = unchecked(Prime1 + Prime2);
        hasher._v2 = Prime2;
        hasher._v3 = 0UL;
        hasher._v4 = unchecked(0UL - Prime1);
        return hasher;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> CarrySpan() => MemoryMarshal.CreateSpan(ref _carry[0], BufferSize);

    /// <summary>
    /// Feeds <paramref name="input"/> into the running hash. Can be called any number of times
    /// with any chunking of the logical input; the final <see cref="Digest"/> result is identical
    /// regardless of how the bytes were split across calls (including splits that land in the
    /// middle of the 32-byte lane block).
    /// </summary>
    public void Append(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty)
        {
            return;
        }

        _totalLength += (ulong)input.Length;
        Span<byte> carry = CarrySpan();

        if (_carryLength > 0)
        {
            int toFill = Math.Min(BufferSize - _carryLength, input.Length);
            input[..toFill].CopyTo(carry[_carryLength..]);
            _carryLength += toFill;
            input = input[toFill..];

            if (_carryLength < BufferSize)
            {
                return;
            }

            ConsumeBlock(carry);
            _carryLength = 0;
        }

        while (input.Length >= BufferSize)
        {
            ConsumeBlock(input);
            input = input[BufferSize..];
        }

        if (!input.IsEmpty)
        {
            input.CopyTo(carry);
            _carryLength = input.Length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConsumeBlock(ReadOnlySpan<byte> block)
    {
        _v1 = Round(_v1, BinaryPrimitives.ReadUInt64LittleEndian(block));
        _v2 = Round(_v2, BinaryPrimitives.ReadUInt64LittleEndian(block[8..]));
        _v3 = Round(_v3, BinaryPrimitives.ReadUInt64LittleEndian(block[16..]));
        _v4 = Round(_v4, BinaryPrimitives.ReadUInt64LittleEndian(block[24..]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Round(ulong accumulator, ulong input)
    {
        accumulator += input * Prime2;
        accumulator = BitOperations.RotateLeft(accumulator, 31);
        accumulator *= Prime1;
        return accumulator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MergeRound(ulong accumulator, ulong value)
    {
        value = Round(0UL, value);
        accumulator ^= value;
        accumulator = accumulator * Prime1 + Prime4;
        return accumulator;
    }

    /// <summary>
    /// Finalizes and returns the hash of every byte fed via <see cref="Append"/> so far. Does not
    /// mutate observable state beyond what a normal read would (safe to call more than once, though
    /// <see cref="StableHashWriter"/> only ever calls it once per instance).
    /// </summary>
    public ulong Digest()
    {
        ulong h64;
        if (_totalLength >= BufferSize)
        {
            h64 = BitOperations.RotateLeft(_v1, 1) + BitOperations.RotateLeft(_v2, 7) +
                  BitOperations.RotateLeft(_v3, 12) + BitOperations.RotateLeft(_v4, 18);
            h64 = MergeRound(h64, _v1);
            h64 = MergeRound(h64, _v2);
            h64 = MergeRound(h64, _v3);
            h64 = MergeRound(h64, _v4);
        }
        else
        {
            h64 = Prime5; // seed (0) + Prime5
        }

        h64 += _totalLength;

        ReadOnlySpan<byte> remaining = CarrySpan()[.._carryLength];
        int offset = 0;

        while (offset + 8 <= remaining.Length)
        {
            ulong lane = Round(0UL, BinaryPrimitives.ReadUInt64LittleEndian(remaining.Slice(offset, 8)));
            h64 ^= lane;
            h64 = BitOperations.RotateLeft(h64, 27) * Prime1 + Prime4;
            offset += 8;
        }

        if (offset + 4 <= remaining.Length)
        {
            h64 ^= BinaryPrimitives.ReadUInt32LittleEndian(remaining.Slice(offset, 4)) * Prime1;
            h64 = BitOperations.RotateLeft(h64, 23) * Prime2 + Prime3;
            offset += 4;
        }

        while (offset < remaining.Length)
        {
            h64 ^= remaining[offset] * Prime5;
            h64 = BitOperations.RotateLeft(h64, 11) * Prime1;
            offset++;
        }

        h64 ^= h64 >> 33;
        h64 *= Prime2;
        h64 ^= h64 >> 29;
        h64 *= Prime3;
        h64 ^= h64 >> 32;

        return h64;
    }
}
