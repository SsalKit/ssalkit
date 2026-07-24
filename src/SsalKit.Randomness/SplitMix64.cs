using System.Runtime.CompilerServices;

namespace SsalKit.Randomness;

/// <summary>
/// SplitMix64 generator, used internally only to expand a single 64-bit seed into the 256-bit
/// xoshiro256** state (and to derive fork seeds). Not exposed as a public <see cref="IRandomSource"/>
/// implementation — it exists purely as a seed-expansion primitive.
/// </summary>
/// <remarks>
/// This is a direct transcription of the reference SplitMix64 algorithm
/// (https://prng.di.unimi.it/splitmix64.c): golden-gamma increment
/// <c>0x9E3779B97F4A7C15</c>, mix constants <c>0xBF58476D1CE4E5B9</c> and
/// <c>0x94D049BB133111EB</c>.
/// </remarks>
internal struct SplitMix64
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15;
    private const ulong MixConstant1 = 0xBF58476D1CE4E5B9;
    private const ulong MixConstant2 = 0x94D049BB133111EB;

    private ulong _state;

    /// <summary>
    /// Initializes a new SplitMix64 instance with the given seed.
    /// </summary>
    /// <param name="seed">The initial state.</param>
    public SplitMix64(ulong seed)
    {
        _state = seed;
    }

    /// <summary>
    /// Advances the generator and returns the next 64-bit output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        ulong z = _state += GoldenGamma;
        z = (z ^ (z >> 30)) * MixConstant1;
        z = (z ^ (z >> 27)) * MixConstant2;
        return z ^ (z >> 31);
    }
}
