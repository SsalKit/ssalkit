using System.Collections;
using System.Collections.Immutable;

namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// A thin wrapper around an immutable array that provides structural (value-based) equality
/// and hashing over its elements.
/// </summary>
/// <remarks>
/// <see cref="ImmutableArray{T}"/> implements <see cref="IEquatable{T}"/> but compares the
/// underlying array by reference, not by content. Records (and the incremental generator
/// pipeline) rely on <see cref="EqualityComparer{T}.Default"/> to decide whether a pipeline
/// stage's output has changed since the previous run. Wrapping arrays in this type ensures two
/// runs that produced the same elements are considered equal, which keeps the generator's
/// caching correct and effective.
/// </remarks>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    public int Length => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => _array[index];

    public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public bool Equals(EquatableArray<T> other)
    {
        var left = _array;
        var right = other._array;

        if (left.IsDefault || right.IsDefault)
        {
            return left.IsDefault == right.IsDefault;
        }

        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array.IsDefault)
        {
            return 0;
        }

        var hash = 17;
        foreach (var item in _array)
        {
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(_array.IsDefault ? ImmutableArray<T>.Empty : _array)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => new(array);
}

internal static class EquatableArray
{
    public static EquatableArray<T> Create<T>(ImmutableArray<T> array)
        where T : IEquatable<T> => new(array);

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T> => new(source.ToImmutableArray());
}
