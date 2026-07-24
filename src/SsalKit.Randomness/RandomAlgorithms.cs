using System.Runtime.CompilerServices;

namespace SsalKit.Randomness;

/// <summary>
/// Source of raw 64-bit values for <see cref="RandomAlgorithms.NextUInt64Bounded{TGenerator}"/>.
/// Implemented as a value type (see <c>DeterministicRandom.Xoshiro256Generator</c>) so the JIT
/// can fully devirtualize and inline the call through a constrained generic type parameter —
/// there is no virtual dispatch and no allocation, unlike a <c>Func&lt;ulong&gt;</c> delegate.
/// </summary>
internal interface IUInt64Generator
{
    /// <summary>
    /// Produces the next uniformly distributed 64-bit unsigned integer.
    /// </summary>
    ulong NextUInt64();
}

/// <summary>
/// Bias-free bounded random generation, factored out of <see cref="DeterministicRandom"/> so the
/// rejection logic exists in exactly one place and can be exercised directly with a stubbed
/// 64-bit source in tests (the range-generating members of <see cref="DeterministicRandom"/> are
/// instance methods and cannot otherwise have their underlying source substituted).
/// </summary>
internal static class RandomAlgorithms
{
    /// <summary>
    /// Produces a uniformly distributed value in <c>[0, bound)</c> using Lemire's
    /// multiply-shift-reject algorithm, sourcing raw 64-bit values from <paramref name="generator"/>.
    /// </summary>
    /// <typeparam name="TGenerator">
    /// A value-type source of raw 64-bit values. Constraining to <see cref="IUInt64Generator"/>
    /// on a generic (rather than accepting the interface directly) lets the JIT specialize this
    /// method per concrete struct type and inline the call, eliminating both virtual dispatch and
    /// delegate-invoke overhead on this hot path.
    /// </typeparam>
    /// <param name="generator">The source of uniformly distributed 64-bit values.</param>
    /// <param name="bound">The exclusive upper bound. Must be greater than zero.</param>
    /// <returns>A value uniformly distributed over <c>[0, bound)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong NextUInt64Bounded<TGenerator>(ref TGenerator generator, ulong bound)
        where TGenerator : IUInt64Generator
    {
        UInt128 product = (UInt128)generator.NextUInt64() * bound;
        ulong low = (ulong)product;

        if (low < bound)
        {
            // The low 64 bits of a bound-multiple product land in [0, bound) with a
            // slightly-too-high probability for the values below this threshold (2^64 mod
            // bound). Rejecting and redrawing when low falls under the threshold removes that
            // modulo bias entirely.
            ulong threshold = unchecked(0UL - bound) % bound;
            while (low < threshold)
            {
                product = (UInt128)generator.NextUInt64() * bound;
                low = (ulong)product;
            }
        }

        return (ulong)(product >> 64);
    }
}
