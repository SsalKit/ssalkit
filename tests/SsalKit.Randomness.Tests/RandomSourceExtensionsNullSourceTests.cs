namespace SsalKit.Randomness.Tests;

/// <summary>
/// Verifies every <see cref="RandomSourceExtensions"/> member rejects a <see langword="null"/>
/// <c>source</c> with <see cref="ArgumentNullException"/>, per the design's uniform null-check
/// requirement (<see cref="ArgumentNullException.ThrowIfNull"/>) applied to every entry point.
/// </summary>
public class RandomSourceExtensionsNullSourceTests
{
    [Fact]
    public void Next_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.Next());
    }

    [Fact]
    public void Next_WithMaxValue_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.Next(10));
    }

    [Fact]
    public void Next_WithMinAndMax_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.Next(1, 10));
    }

    [Fact]
    public void NextInt64_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.NextInt64());
    }

    [Fact]
    public void NextInt64_WithMaxValue_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.NextInt64(10L));
    }

    [Fact]
    public void NextInt64_WithMinAndMax_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.NextInt64(1L, 10L));
    }

    [Fact]
    public void NextDouble_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.NextDouble());
    }

    [Fact]
    public void NextSingle_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.NextSingle());
    }

    [Fact]
    public void NextBoolean_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;

        Assert.Throws<ArgumentNullException>(() => source!.NextBoolean());
    }

    [Fact]
    public void Shuffle_Span_NullSource_ThrowsArgumentNullException()
    {
        // The span is constructed inside the lambda body (rather than captured from an outer
        // local) because Span<T>, being a ref struct, cannot be captured by a closure.
        IRandomSource? source = null;
        int[] values = [1, 2, 3];

        Assert.Throws<ArgumentNullException>(() => source!.Shuffle(values.AsSpan()));
    }

    [Fact]
    public void Shuffle_IList_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        List<int> values = [1, 2, 3];

        Assert.Throws<ArgumentNullException>(() => source!.Shuffle(values));
    }

    [Fact]
    public void Pick_Span_NullSource_ThrowsArgumentNullException()
    {
        // The span is constructed inside the lambda body (rather than captured from an outer
        // local) because ReadOnlySpan<T>, being a ref struct, cannot be captured by a closure.
        IRandomSource? source = null;
        int[] items = [1, 2, 3];

        Assert.Throws<ArgumentNullException>(() => source!.Pick(items.AsSpan()));
    }

    [Fact]
    public void Pick_IReadOnlyList_NullSource_ThrowsArgumentNullException()
    {
        IRandomSource? source = null;
        IReadOnlyList<int> items = [1, 2, 3];

        Assert.Throws<ArgumentNullException>(() => source!.Pick(items));
    }
}
