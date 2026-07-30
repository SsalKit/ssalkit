namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Verifies the equality-consistency invariant from design doc §4.2 — "for every supported type,
/// <c>a == b</c> implies <c>encode(a) == encode(b)</c>" — for every row of its trap table (design
/// doc §6 test 3): decimal scale variants, <see cref="DateTimeOffset"/> instants under different
/// offsets, floating-point negative zero, and differing NaN bit patterns.
/// </summary>
/// <remarks>
/// Note on scope: design doc §4.2's <c>ImmutableArray&lt;T&gt;</c> default-vs-empty row is a
/// generator-level concern (the generator must call <see cref="StableHashWriter.AppendCount"/> with
/// 0 for both cases), not something <see cref="StableHashWriter"/> itself encodes specially — there
/// is no <c>AppendImmutableArray</c> method at this layer, so it is not covered here.
/// </remarks>
public class EqualityConsistencyTests
{
    // StableHashWriter is a ref struct, so it cannot be captured by a delegate (e.g.
    // Action<StableHashWriter>) or stored in a field — each test below builds and finishes its own
    // writer inline instead of going through a shared helper.

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.00")]
    [InlineData("1.000")]
    [InlineData("1.0000000000000000000000000000")] // scale 28, still normalizes to the same mantissa/scale as "1"
    public void AppendDecimal_TrailingZeroVariants_ProduceSameHash(string literal)
    {
        decimal value = decimal.Parse(literal, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(1.0m, value); // sanity: still the same decimal value under ==

        StableHashWriter baseline = StableHashWriter.Create();
        baseline.AppendDecimal(1.0m);
        ulong expected = baseline.Finish().Value;

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendDecimal(value);
        ulong actual = writer.Finish().Value;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppendDecimal_NegativeTrailingZeroVariants_ProduceSameHash()
    {
        StableHashWriter baseline = StableHashWriter.Create();
        baseline.AppendDecimal(-2.5m);
        ulong expected = baseline.Finish().Value;

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendDecimal(-2.500m);
        ulong actual = writer.Finish().Value;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppendDecimal_MaxScale28_NormalizesConsistentlyWithLowerScale()
    {
        // 28 is the maximum decimal scale. 0.1000000000000000000000000000m (scale 28) must
        // normalize to the same encoding as 0.1m (scale 1).
        decimal maxScale = 0.1000000000000000000000000000m;
        Assert.Equal(0.1m, maxScale);

        StableHashWriter baseline = StableHashWriter.Create();
        baseline.AppendDecimal(0.1m);
        ulong expected = baseline.Finish().Value;

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendDecimal(maxScale);
        ulong actual = writer.Finish().Value;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppendDecimal_ZeroVariants_ProduceSameHash()
    {
        StableHashWriter baseline = StableHashWriter.Create();
        baseline.AppendDecimal(0m);
        ulong expected = baseline.Finish().Value;

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendDecimal(0.00m);
        ulong actual = writer.Finish().Value;

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Negative zero and every other all-zero <see langword="decimal"/> representation must encode
    /// identically: <c>decimal.GetBits</c> preserves a sign flag for negative zero even though
    /// <c>-0.0m == 0.0m</c> under decimal equality, so <see cref="StableHashWriter.AppendDecimal"/>
    /// must normalize the sign away for every zero, not just the scale.
    /// </summary>
    /// <remarks>
    /// Each case is its own <see cref="FactAttribute"/> rather than a <see cref="TheoryAttribute"/>
    /// with decimal <c>MemberData</c>: several of these zero variants (e.g. <c>-0.0m</c> and
    /// <c>decimal.Negate(0m)</c>) serialize to an identical xunit test-case display name/ID, which
    /// causes xunit to silently skip the "duplicate" case under a shared theory.
    /// </remarks>
    [Fact]
    public void AppendDecimal_NegativeZeroLiteral_ProducesCanonicalZeroHash() =>
        AssertNormalizesToCanonicalZero(-0.0m);

    [Fact]
    public void AppendDecimal_NegateOfZero_ProducesCanonicalZeroHash() =>
        AssertNormalizesToCanonicalZero(decimal.Negate(0m));

    [Fact]
    public void AppendDecimal_NegativeZeroNearMaxScale_ProducesCanonicalZeroHash() =>
        AssertNormalizesToCanonicalZero(-0.000000000000000000000000000m); // scale 27, negative zero

    [Fact]
    public void AppendDecimal_PositiveZeroWithScale_ProducesCanonicalZeroHash() =>
        AssertNormalizesToCanonicalZero(0.00m);

    private static void AssertNormalizesToCanonicalZero(decimal value)
    {
        Assert.Equal(0m, value);

        StableHashWriter baseline = StableHashWriter.Create();
        baseline.AppendDecimal(0m);
        ulong expected = baseline.Finish().Value;

        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendDecimal(value);
        ulong actual = writer.Finish().Value;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppendDecimal_Zero_StillDiffersFromNonZero()
    {
        // Guards against an overzealous fix collapsing every decimal into the zero encoding.
        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDecimal(0m);
        ulong zero = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDecimal(0.1m);
        ulong nonZero = w2.Finish().Value;

        Assert.NotEqual(zero, nonZero);
    }

    [Fact]
    public void AppendDateTimeOffset_SameInstantDifferentOffset_ProduceSameHash()
    {
        var utc = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var plusOne = new DateTimeOffset(2026, 7, 30, 13, 0, 0, TimeSpan.FromHours(1));
        var minusFive = new DateTimeOffset(2026, 7, 30, 7, 0, 0, TimeSpan.FromHours(-5));
        Assert.Equal(utc, plusOne);
        Assert.Equal(utc, minusFive);

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDateTimeOffset(utc);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDateTimeOffset(plusOne);
        ulong h2 = w2.Finish().Value;

        StableHashWriter w3 = StableHashWriter.Create();
        w3.AppendDateTimeOffset(minusFive);
        ulong h3 = w3.Finish().Value;

        Assert.Equal(h1, h2);
        Assert.Equal(h1, h3);
    }

    [Fact]
    public void AppendSingle_NegativeAndPositiveZero_ProduceSameHash()
    {
        Assert.Equal(-0f, 0f);

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendSingle(-0f);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendSingle(0f);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendDouble_NegativeAndPositiveZero_ProduceSameHash()
    {
        Assert.Equal(-0d, 0d);

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDouble(-0d);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDouble(0d);
        ulong h2 = w2.Finish().Value;

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AppendSingle_DifferentNaNBitPatterns_ProduceSameHash()
    {
        float quietNaN = BitConverter.Int32BitsToSingle(unchecked((int)0x7FC00000));
        float signalingNaN = BitConverter.Int32BitsToSingle(unchecked((int)0x7FA00001));
        float negativeNaN = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC12345));
        Assert.True(float.IsNaN(quietNaN));
        Assert.True(float.IsNaN(signalingNaN));
        Assert.True(float.IsNaN(negativeNaN));
        Assert.NotEqual(BitConverter.SingleToInt32Bits(quietNaN), BitConverter.SingleToInt32Bits(signalingNaN));
        Assert.NotEqual(BitConverter.SingleToInt32Bits(quietNaN), BitConverter.SingleToInt32Bits(negativeNaN));

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendSingle(quietNaN);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendSingle(signalingNaN);
        ulong h2 = w2.Finish().Value;

        StableHashWriter w3 = StableHashWriter.Create();
        w3.AppendSingle(negativeNaN);
        ulong h3 = w3.Finish().Value;

        Assert.Equal(h1, h2);
        Assert.Equal(h1, h3);
    }

    [Fact]
    public void AppendDouble_DifferentNaNBitPatterns_ProduceSameHash()
    {
        double quietNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000L);
        double signalingNaN = BitConverter.Int64BitsToDouble(0x7FF4000000000001L);
        double negativeNaN = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8123456789ABCUL));
        Assert.True(double.IsNaN(quietNaN));
        Assert.True(double.IsNaN(signalingNaN));
        Assert.True(double.IsNaN(negativeNaN));
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(quietNaN), BitConverter.DoubleToInt64Bits(signalingNaN));
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(quietNaN), BitConverter.DoubleToInt64Bits(negativeNaN));

        StableHashWriter w1 = StableHashWriter.Create();
        w1.AppendDouble(quietNaN);
        ulong h1 = w1.Finish().Value;

        StableHashWriter w2 = StableHashWriter.Create();
        w2.AppendDouble(signalingNaN);
        ulong h2 = w2.Finish().Value;

        StableHashWriter w3 = StableHashWriter.Create();
        w3.AppendDouble(negativeNaN);
        ulong h3 = w3.Finish().Value;

        Assert.Equal(h1, h2);
        Assert.Equal(h1, h3);
    }
}
