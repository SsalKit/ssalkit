using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace SsalKit.Randomness;

/// <summary>
/// A deterministic, state-serializable pseudo-random number generator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Security warning: this generator is predictable.</b> Given a handful of consecutive
/// outputs, its internal state can be reconstructed and every future (and, with the xoshiro256**
/// algorithm's short back-tracking window, past) output predicted. Never use
/// <see cref="DeterministicRandom"/> for tokens, credentials, shuffling a deck whose order must
/// stay secret, or any other security-sensitive purpose. Use <c>CryptoRandomSource</c> for those.
/// </para>
/// <para>
/// <b>Not thread-safe.</b> A single instance must not be accessed from multiple threads without
/// external synchronization; concurrent access can corrupt the internal state and will break
/// sequence reproducibility.
/// </para>
/// <para>
/// <b>Algorithm contract (v1):</b> the output sequence is xoshiro256** and seed expansion is
/// SplitMix64, with a 256-bit state (four <see cref="ulong"/> words, exposed as
/// <see cref="RandomState"/>). This contract is permanently fixed for this type: a given seed or
/// state will always produce the same sequence, on any platform, in any process, forever.
/// Because <see cref="RandomState"/> can be persisted as save data, any change to the output
/// sequence would be a form of data corruption for consumers — so it will never happen in a
/// patch or minor release. Algorithmic evolution, if it ever occurs, will ship as a new type
/// (e.g. a hypothetical <c>DeterministicRandomV2</c>) rather than by changing this one.
/// </para>
/// </remarks>
public sealed class DeterministicRandom : IRandomSource
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>
    /// Initializes a new instance seeded from a single 64-bit value. The 256-bit internal state
    /// is expanded from <paramref name="seed"/> using SplitMix64, which guarantees the resulting
    /// state can never be the invalid all-zero state.
    /// </summary>
    /// <param name="seed">The seed value. Any value, including 0, is valid.</param>
    public DeterministicRandom(ulong seed)
    {
        var splitMix64 = new SplitMix64(seed);
        _s0 = splitMix64.Next();
        _s1 = splitMix64.Next();
        _s2 = splitMix64.Next();
        _s3 = splitMix64.Next();
    }

    private DeterministicRandom(RandomState state)
    {
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    /// <summary>
    /// Value-type adapter over an owning <see cref="DeterministicRandom"/>, implementing
    /// <see cref="IUInt64Generator"/> so <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/>
    /// can be called with this concrete struct type. Because the type argument is a struct, the
    /// JIT specializes and fully inlines the call — no virtual dispatch, no delegate, no
    /// allocation, unlike routing through a cached <c>Func&lt;ulong&gt;</c>.
    /// </summary>
    private readonly struct Xoshiro256Generator(DeterministicRandom owner) : IUInt64Generator
    {
        public ulong NextUInt64() => owner.NextUInt64();
    }

    /// <summary>
    /// Creates a new instance whose full internal state is restored from a previously exported
    /// <see cref="RandomState"/>. The resulting instance continues the exact sequence that would
    /// have followed <paramref name="state"/> in the original instance.
    /// </summary>
    /// <param name="state">A previously exported state.</param>
    /// <returns>A new instance with the restored state.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="state"/> is the all-zero state (see <see cref="RandomState.IsValid"/>).
    /// </exception>
    public static DeterministicRandom FromState(RandomState state)
    {
        if (!state.IsValid)
        {
            throw new ArgumentException("The all-zero state is not a valid xoshiro256** state.", nameof(state));
        }

        return new DeterministicRandom(state);
    }

    /// <summary>
    /// Creates a new instance seeded unpredictably, using a cryptographic random number
    /// generator to produce the seed. The instance itself remains a predictable
    /// <see cref="DeterministicRandom"/> once created — only the seed is unpredictable.
    /// </summary>
    /// <returns>A new, randomly seeded instance.</returns>
    public static DeterministicRandom CreateRandomlySeeded()
    {
        Span<byte> seedBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(seedBytes);
        ulong seed = BinaryPrimitives.ReadUInt64LittleEndian(seedBytes);
        return new DeterministicRandom(seed);
    }

    /// <summary>
    /// Exports the full 256-bit internal state, suitable for persistence and later restoration
    /// via <see cref="FromState(RandomState)"/>.
    /// </summary>
    /// <returns>The current internal state.</returns>
    public RandomState ExportState() => new(_s0, _s1, _s2, _s3);

    /// <summary>
    /// Creates a new, independent generator derived from this one, advancing this instance's
    /// state by exactly one step in the process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The contract is exactly <c>Fork() == new DeterministicRandom(this.NextUInt64())</c>: a
    /// single 64-bit value is drawn from this instance (which is indistinguishable from any
    /// other call to <see cref="NextUInt64"/> and leaves this instance's sequence otherwise
    /// unbroken) and expanded into the child's 256-bit state via SplitMix64, exactly as the
    /// <see cref="DeterministicRandom(ulong)"/> constructor would.
    /// </para>
    /// <para>
    /// "Independent" here means the child's stream is derived from a distinct 64-bit seed, not
    /// that distinct children are provably disjoint. Because the seed is 64 bits, two children
    /// forked from unrelated parents can in principle collide, and by the birthday bound that
    /// becomes non-negligible only around 2^32 forks — far beyond the scale of any game or
    /// simulation workload, but worth knowing before treating fork counts in that range as safe.
    /// </para>
    /// </remarks>
    /// <returns>A new, independent <see cref="DeterministicRandom"/>.</returns>
    public DeterministicRandom Fork() => new(NextUInt64());

    /// <summary>
    /// Produces the next uniformly distributed 64-bit unsigned integer using the xoshiro256**
    /// algorithm.
    /// </summary>
    /// <returns>A value uniformly distributed over the full range of <see cref="ulong"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        ulong s0 = _s0;
        ulong s1 = _s1;
        ulong s2 = _s2;
        ulong s3 = _s3;

        ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;

        ulong t = s1 << 17;

        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;

        s2 ^= t;

        s3 = BitOperations.RotateLeft(s3, 45);

        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;

        return result;
    }

    /// <summary>
    /// Returns a non-negative random integer in the range <c>[0, int.MaxValue)</c>.
    /// </summary>
    /// <returns>A value in <c>[0, int.MaxValue)</c>. <see cref="int.MaxValue"/> itself is never returned.</returns>
    public int Next()
    {
        var generator = new Xoshiro256Generator(this);
        return (int)RandomAlgorithms.NextUInt64Bounded(ref generator, int.MaxValue);
    }

    /// <summary>
    /// Returns a non-negative random integer in the range <c>[0, maxValue)</c>.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound. Must be non-negative.</param>
    /// <returns>A value in <c>[0, maxValue)</c>, or 0 if <paramref name="maxValue"/> is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is negative.</exception>
    public int Next(int maxValue)
    {
        if (maxValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "maxValue must be non-negative.");
        }

        if (maxValue == 0)
        {
            return 0;
        }

        var generator = new Xoshiro256Generator(this);
        return (int)RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)maxValue);
    }

    /// <summary>
    /// Returns a random integer in the range <c>[minValue, maxValue)</c>.
    /// </summary>
    /// <param name="minValue">The inclusive lower bound.</param>
    /// <param name="maxValue">The exclusive upper bound.</param>
    /// <returns>
    /// A value in <c>[minValue, maxValue)</c>, or <paramref name="minValue"/> if the two bounds
    /// are equal.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    public int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "minValue must be less than or equal to maxValue.");
        }

        if (minValue == maxValue)
        {
            return minValue;
        }

        ulong range = (ulong)((long)maxValue - (long)minValue);
        var generator = new Xoshiro256Generator(this);
        return minValue + (int)RandomAlgorithms.NextUInt64Bounded(ref generator, range);
    }

    /// <summary>
    /// Returns a non-negative random 64-bit integer in the range <c>[0, long.MaxValue)</c>.
    /// </summary>
    /// <returns>A value in <c>[0, long.MaxValue)</c>. <see cref="long.MaxValue"/> itself is never returned.</returns>
    public long NextInt64()
    {
        var generator = new Xoshiro256Generator(this);
        return (long)RandomAlgorithms.NextUInt64Bounded(ref generator, long.MaxValue);
    }

    /// <summary>
    /// Returns a non-negative random 64-bit integer in the range <c>[0, maxValue)</c>.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound. Must be non-negative.</param>
    /// <returns>A value in <c>[0, maxValue)</c>, or 0 if <paramref name="maxValue"/> is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is negative.</exception>
    public long NextInt64(long maxValue)
    {
        if (maxValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "maxValue must be non-negative.");
        }

        if (maxValue == 0)
        {
            return 0;
        }

        var generator = new Xoshiro256Generator(this);
        return (long)RandomAlgorithms.NextUInt64Bounded(ref generator, (ulong)maxValue);
    }

    /// <summary>
    /// Returns a random 64-bit integer in the range <c>[minValue, maxValue)</c>.
    /// </summary>
    /// <param name="minValue">The inclusive lower bound.</param>
    /// <param name="maxValue">The exclusive upper bound.</param>
    /// <returns>
    /// A value in <c>[minValue, maxValue)</c>, or <paramref name="minValue"/> if the two bounds
    /// are equal.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    public long NextInt64(long minValue, long maxValue)
    {
        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "minValue must be less than or equal to maxValue.");
        }

        if (minValue == maxValue)
        {
            return minValue;
        }

        // Two's-complement subtraction wraps correctly here even for the extreme case
        // minValue = long.MinValue, maxValue = long.MaxValue (an exclusive range of 2^64 - 1),
        // which would overflow if computed in signed arithmetic.
        ulong range = unchecked((ulong)maxValue - (ulong)minValue);
        var generator = new Xoshiro256Generator(this);
        ulong offset = RandomAlgorithms.NextUInt64Bounded(ref generator, range);
        return unchecked(minValue + (long)offset);
    }

    /// <summary>
    /// Returns a random floating-point number in the range <c>[0, 1)</c>, with 53 bits of
    /// precision.
    /// </summary>
    /// <returns>A value in <c>[0, 1)</c>. 1.0 is never returned.</returns>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// Returns a random single-precision floating-point number in the range <c>[0, 1)</c>, with
    /// 24 bits of precision.
    /// </summary>
    /// <returns>A value in <c>[0, 1)</c>. 1.0f is never returned.</returns>
    public float NextSingle() => (NextUInt64() >> 40) * (1.0f / (1UL << 24));

    /// <summary>
    /// Returns a random boolean, derived from the most significant bit of a
    /// <see cref="NextUInt64"/> draw.
    /// </summary>
    /// <returns><see langword="true"/> or <see langword="false"/>, each with probability 0.5.</returns>
    public bool NextBoolean() => (NextUInt64() >> 63) != 0;

    /// <summary>
    /// Fills <paramref name="buffer"/> with random bytes, deterministically derived from this
    /// instance's sequence.
    /// </summary>
    /// <remarks>
    /// The buffer is filled 8 bytes at a time from successive <see cref="NextUInt64"/> draws,
    /// each written in little-endian order; if the buffer's length is not a multiple of 8, the
    /// final partial chunk takes the low-order bytes of one additional draw.
    /// </remarks>
    /// <param name="buffer">The buffer to fill. May be empty.</param>
    public void NextBytes(Span<byte> buffer)
    {
        int fullChunks = buffer.Length / 8;
        int i = 0;
        for (; i < fullChunks; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(i * 8, 8), NextUInt64());
        }

        int remaining = buffer.Length - (i * 8);
        if (remaining > 0)
        {
            Span<byte> tail = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(tail, NextUInt64());
            tail[..remaining].CopyTo(buffer[(i * 8)..]);
        }
    }
}
