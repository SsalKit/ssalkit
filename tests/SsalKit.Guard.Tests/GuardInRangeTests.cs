using System.Globalization;

namespace SsalKit.Guard.Tests;

public sealed class GuardInRangeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    public void InRange_ValueWithinTheInclusiveRange_ReturnsIt(int value)
    {
        int returned = Guard.InRange(value, 1, 99);

        Assert.Equal(value, returned);
    }

    [Fact]
    public void InRange_ValueBelowTheLowerBound_MessageEmbedsTheExpressionAndTheBounds()
    {
        int level = 0;

        var exception = Assert.Throws<GuardViolationException>(() => Guard.InRange(level, 1, 99));

        Assert.Equal(
            "Guard.InRange (level) failed: value 0 was outside the inclusive range [1, 99].",
            exception.Message);
    }

    [Fact]
    public void InRange_ValueAboveTheUpperBound_MessageEmbedsTheExpressionAndTheBounds()
    {
        int level = 120;

        var exception = Assert.Throws<GuardViolationException>(() => Guard.InRange(level, 1, 99));

        Assert.Equal(
            "Guard.InRange (level) failed: value 120 was outside the inclusive range [1, 99].",
            exception.Message);
    }

    [Fact]
    public void InRange_WithNullExpression_UsesThePlaceholder()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.InRange(0, 1, 99, expression: null));

        Assert.Equal(
            $"Guard.InRange ({Guard.UnknownExpression}) failed: value 0 was outside the inclusive range [1, 99].",
            exception.Message);
    }

    [Fact]
    public void InRange_WorksForAnyComparable()
    {
        string returned = Guard.InRange("m", "a", "z");

        Assert.Equal("m", returned);
    }

    /// <summary>
    /// The message renders through the invariant culture, so a failure reads identically wherever
    /// it is thrown — a culture that uses ',' as its decimal separator must not change it.
    /// </summary>
    [Fact]
    public void InRange_RendersBoundsWithTheInvariantCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            double ratio = 2.5;

            var exception = Assert.Throws<GuardViolationException>(
                () => Guard.InRange(ratio, 0.5, 1.5));

            Assert.Equal(
                "Guard.InRange (ratio) failed: value 2.5 was outside the inclusive range [0.5, 1.5].",
                exception.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
