namespace SsalKit.Timekeeping.Tests;

public sealed class TimeWindowTests
{
    private static readonly DateTimeOffset Noon = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static TimeWindow Window(int startHour, int endHour) =>
        new(Noon.AddHours(startHour), Noon.AddHours(endHour));

    [Fact]
    public void Constructor_Throws_WhenEndPrecedesStart()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TimeWindow(Noon, Noon.AddTicks(-1)));
        Assert.Equal("end", exception.ParamName);
    }

    [Fact]
    public void Constructor_Allows_EmptyWindow()
    {
        var window = new TimeWindow(Noon, Noon);

        Assert.Equal(TimeSpan.Zero, window.Duration);
        Assert.False(window.Contains(Noon));
    }

    [Fact]
    public void Properties_RoundTripTheBounds()
    {
        var window = new TimeWindow(Noon, Noon.AddHours(6));

        Assert.Equal(Noon, window.Start);
        Assert.Equal(Noon.AddHours(6), window.End);
        Assert.Equal(TimeSpan.FromHours(6), window.Duration);
    }

    [Fact]
    public void Contains_IsHalfOpen()
    {
        var window = Window(0, 2);

        Assert.False(window.Contains(Noon.AddTicks(-1)));
        Assert.True(window.Contains(Noon));
        Assert.True(window.Contains(Noon.AddHours(2).AddTicks(-1)));
        Assert.False(window.Contains(Noon.AddHours(2)));
        Assert.False(window.Contains(Noon.AddHours(3)));
    }

    [Fact]
    public void AdjacentWindows_NeitherOverlapNorLeak()
    {
        var first = Window(0, 1);
        var second = Window(1, 2);
        var shared = Noon.AddHours(1);

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
        Assert.Null(first.Intersect(second));

        // The shared instant belongs to exactly one of the two windows.
        Assert.False(first.Contains(shared));
        Assert.True(second.Contains(shared));
    }

    [Fact]
    public void Contains_ComparesAbsoluteInstants_NotOffsetNotation()
    {
        var window = new TimeWindow(
            new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero));

        // 2026-07-25T09:00+09:00 is 2026-07-25T00:00Z — the very start of the window.
        Assert.True(window.Contains(new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.FromHours(9))));

        // 2026-07-26T09:00+09:00 is 2026-07-26T00:00Z — the exclusive end.
        Assert.False(window.Contains(new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(9))));
    }

    [Fact]
    public void Equality_ComparesAbsoluteInstants_NotOffsetNotation()
    {
        var utc = new TimeWindow(
            new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero));
        var seoul = new TimeWindow(
            new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.FromHours(9)),
            new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(9)));

        Assert.Equal(utc, seoul);
        Assert.True(utc == seoul);
        Assert.False(utc != seoul);
        Assert.Equal(utc.GetHashCode(), seoul.GetHashCode());

        // ...but the offsets are preserved for display.
        Assert.Equal(TimeSpan.FromHours(9), seoul.Start.Offset);
        Assert.Contains("Start", seoul.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Equality_DistinguishesDifferentIntervals()
    {
        Assert.NotEqual(Window(0, 1), Window(0, 2));
        Assert.NotEqual(Window(0, 2), Window(1, 2));
    }

    [Theory]
    [InlineData(0, 4, 1, 3, 1, 3)]   // fully contained
    [InlineData(1, 3, 0, 4, 1, 3)]   // fully containing
    [InlineData(0, 3, 1, 4, 1, 3)]   // overlapping tail
    [InlineData(1, 4, 0, 3, 1, 3)]   // overlapping head
    [InlineData(0, 2, 0, 2, 0, 2)]   // identical
    public void Intersect_IsSymmetricAndExact(
        int firstStart,
        int firstEnd,
        int secondStart,
        int secondEnd,
        int expectedStart,
        int expectedEnd)
    {
        var first = Window(firstStart, firstEnd);
        var second = Window(secondStart, secondEnd);
        var expected = Window(expectedStart, expectedEnd);

        Assert.Equal(expected, first.Intersect(second));
        Assert.Equal(expected, second.Intersect(first));
        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
    }

    [Fact]
    public void Intersect_ReturnsNull_WhenDisjoint()
    {
        Assert.Null(Window(0, 1).Intersect(Window(2, 3)));
        Assert.Null(Window(2, 3).Intersect(Window(0, 1)));
    }

    [Fact]
    public void Intersect_ReturnsNull_ForAnEmptyWindowInsideAnother()
    {
        var empty = Window(1, 1);
        var enclosing = Window(0, 2);

        Assert.Null(empty.Intersect(enclosing));
        Assert.Null(enclosing.Intersect(empty));
        Assert.False(empty.Overlaps(enclosing));
        Assert.False(enclosing.Overlaps(empty));
    }

    [Fact]
    public void Clamp_RestrictsToTheClosedRange()
    {
        var window = Window(0, 2);

        Assert.Equal(window.Start, window.Clamp(Noon.AddHours(-1)));
        Assert.Equal(window.Start, window.Clamp(window.Start));
        Assert.Equal(Noon.AddHours(1), window.Clamp(Noon.AddHours(1)));
        Assert.Equal(window.End, window.Clamp(window.End));
        Assert.Equal(window.End, window.Clamp(Noon.AddHours(5)));
    }
}
