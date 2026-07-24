namespace SsalKit.Randomness.Tests;

/// <summary>
/// An independent, direct transcription of the public reference algorithms — xoshiro256**
/// (https://prng.di.unimi.it/xoshiro256starstar.c) and SplitMix64
/// (https://prng.di.unimi.it/splitmix64.c) — kept deliberately separate from
/// <see cref="SsalKit.Randomness.DeterministicRandom"/> and <c>SsalKit.Randomness.SplitMix64</c>
/// so that golden-vector tests compare production code against a second, independently written
/// implementation rather than against itself.
/// </summary>
internal static class ReferenceXoshiro256StarStar
{
    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

    /// <summary>
    /// Runs the reference SplitMix64 generator for <paramref name="count"/> steps starting from
    /// <paramref name="seed"/> and returns each successive output.
    /// </summary>
    public static ulong[] SplitMix64Sequence(ulong seed, int count)
    {
        ulong state = seed;
        var results = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            results[i] = z ^ (z >> 31);
        }

        return results;
    }

    /// <summary>
    /// Expands <paramref name="seed"/> into a 256-bit xoshiro256** state via four SplitMix64
    /// draws, then runs the reference xoshiro256** generator for <paramref name="count"/> steps
    /// and returns each successive output.
    /// </summary>
    public static ulong[] NextUInt64Sequence(ulong seed, int count)
    {
        ulong[] seedWords = SplitMix64Sequence(seed, 4);
        ulong s0 = seedWords[0];
        ulong s1 = seedWords[1];
        ulong s2 = seedWords[2];
        ulong s3 = seedWords[3];

        var results = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            ulong result = RotateLeft(s1 * 5, 7) * 9;

            ulong t = s1 << 17;

            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;

            s2 ^= t;

            s3 = RotateLeft(s3, 45);

            results[i] = result;
        }

        return results;
    }
}
