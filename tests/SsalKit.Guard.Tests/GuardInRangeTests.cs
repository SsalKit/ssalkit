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
    /// A <see langword="null"/> value is a value that is not in the range, so it fails the way any
    /// other out-of-range value does rather than with a bare <see cref="NullReferenceException"/>
    /// from the first comparison.
    /// </summary>
    [Fact]
    public void InRange_NullValue_ThrowsAGuardViolation()
    {
        string? name = null;

        var exception = Assert.Throws<GuardViolationException>(() => Guard.InRange(name!, "a", "z"));

        Assert.Equal("Guard.InRange (name) failed: value was null.", exception.Message);
    }

    /// <summary>
    /// Bounds are the check's own configuration, not the domain value under test, so bounds that
    /// cannot describe any range are a programming error and say so with an
    /// <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void InRange_MinGreaterThanMax_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(static () => Guard.InRange(5, 99, 1));

        Assert.Equal("min", exception.ParamName);
        Assert.Contains("The range [99, 1] is empty", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it is raised even when the value itself would have passed a well-formed range, because
    /// the range is what is wrong.
    /// </summary>
    [Fact]
    public void InRange_MinGreaterThanMax_IsReportedBeforeTheValueIsLookedAt()
    {
        Assert.Throws<ArgumentException>(static () => Guard.InRange(50, 99, 1));
    }

    [Fact]
    public void InRange_NullLowerBound_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(static () => Guard.InRange("m", null!, "z"));

        Assert.Equal("min", exception.ParamName);
    }

    [Fact]
    public void InRange_NullUpperBound_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(static () => Guard.InRange("m", "a", null!));

        Assert.Equal("max", exception.ParamName);
    }

    /// <summary>
    /// <c>NaN</c> is in no range, and <see cref="double.CompareTo(double)"/> orders it below every
    /// other value, so it always fails the lower-bound comparison.
    /// </summary>
    [Fact]
    public void InRange_NaNValue_AlwaysFails()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.InRange(double.NaN, 0.0, 1.0));

        Assert.Contains("value NaN was outside", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>NaN</c> lower bound is not a broken range for the same reason: it compares below the
    /// upper bound, so it reads as a bound nothing can fall under.
    /// </summary>
    [Fact]
    public void InRange_NaNLowerBound_IsNotTreatedAsAnEmptyRange()
    {
        double returned = Guard.InRange(0.5, double.NaN, 1.0);

        Assert.Equal(0.5, returned);
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
