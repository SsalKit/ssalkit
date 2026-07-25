using System.Collections;
using System.Collections.Immutable;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="EquatableArray{T}"/>, covering the value-equality semantics
/// (including the <c>default</c>/uninitialized state) that an incremental generator pipeline
/// relies on for correct caching.
/// </summary>
public class EquatableArrayTests
{
    [Fact]
    public void Equals_BothDefault_ReturnsTrue()
    {
        var left = default(EquatableArray<string>);
        var right = default(EquatableArray<string>);

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Equals_DefaultVersusNonDefault_ReturnsFalse()
    {
        var left = default(EquatableArray<string>);
        var right = new[] { "a" }.ToEquatableArray();

        Assert.False(left.Equals(right));
        Assert.False(right.Equals(left));
    }

    [Fact]
    public void Equals_DifferentLengths_ReturnsFalse()
    {
        var left = new[] { "a" }.ToEquatableArray();
        var right = new[] { "a", "b" }.ToEquatableArray();

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_SameLengthDifferentElement_ReturnsFalse()
    {
        var left = new[] { "a", "b" }.ToEquatableArray();
        var right = new[] { "a", "c" }.ToEquatableArray();

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_SameLengthSameElements_ReturnsTrue()
    {
        var left = new[] { "a", "b" }.ToEquatableArray();
        var right = new[] { "a", "b" }.ToEquatableArray();

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void EqualsObject_SameValueBoxedAsObject_ReturnsTrue()
    {
        var array = new[] { "a", "b" }.ToEquatableArray();
        object boxed = new[] { "a", "b" }.ToEquatableArray();

        Assert.True(array.Equals(boxed));
    }

    [Fact]
    public void EqualsObject_DifferentType_ReturnsFalse()
    {
        var array = new[] { "a", "b" }.ToEquatableArray();
        object other = "string";

        Assert.False(array.Equals(other));
    }

    [Fact]
    public void GetHashCode_SameContent_ProducesSameHash()
    {
        var left = new[] { "a", "b" }.ToEquatableArray();
        var right = new[] { "a", "b" }.ToEquatableArray();

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_Default_IsZero()
    {
        var array = default(EquatableArray<string>);

        Assert.Equal(0, array.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ArrayContainingNullElement_TreatsNullAsZeroContribution()
    {
        // Exercises the null-conditional branch of "item?.GetHashCode() ?? 0" for reference-type
        // elements, alongside the non-null branch already covered by the other GetHashCode tests.
        // The null-forgiving operator keeps the array's static element type as non-nullable
        // "string" (satisfying EquatableArray<T>'s "where T : IEquatable<T>" constraint) while
        // still storing an actual null reference at runtime.
        var source = new[] { "a", null! };
        var withNull = source.ToEquatableArray();
        var withoutSecondElement = new[] { "a" }.ToEquatableArray();

        // hash = ((17 * 31) + "a".GetHashCode()) * 31 + 0, i.e. one extra "* 31" step versus the
        // single-element array, since a null element contributes 0 instead of its hash code.
        var expected = (withoutSecondElement.GetHashCode() * 31) + 0;

        Assert.Equal(expected, withNull.GetHashCode());
    }

    [Fact]
    public void EqualityOperators_MatchEqualsResult()
    {
        var left = new[] { "a", "b" }.ToEquatableArray();
        var right = new[] { "a", "b" }.ToEquatableArray();
        var different = new[] { "a", "c" }.ToEquatableArray();

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.False(left == different);
        Assert.True(left != different);
    }

    [Fact]
    public void GenericEnumerator_YieldsElementsInOrder()
    {
        var array = new[] { "a", "b", "c" }.ToEquatableArray();

        var items = new List<string>();
        foreach (var item in array)
        {
            items.Add(item);
        }

        Assert.Equal(new[] { "a", "b", "c" }, items);
    }

    [Fact]
    public void NonGenericEnumerator_YieldsElementsInOrder()
    {
        IEnumerable array = new[] { "a", "b", "c" }.ToEquatableArray();

        var items = new List<object?>();
        var enumerator = array.GetEnumerator();
        while (enumerator.MoveNext())
        {
            items.Add(enumerator.Current);
        }

        Assert.Equal(new object?[] { "a", "b", "c" }, items);
    }

    [Fact]
    public void Enumeration_Default_YieldsEmptySequenceWithoutThrowing()
    {
        var array = default(EquatableArray<string>);

        var items = new List<string>();
        foreach (var item in array)
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    [Fact]
    public void Length_And_Indexer_ReflectUnderlyingElements()
    {
        var array = new[] { "a", "b", "c" }.ToEquatableArray();

        Assert.Equal(3, array.Length);
        Assert.Equal("a", array[0]);
        Assert.Equal("b", array[1]);
        Assert.Equal("c", array[2]);
    }

    [Fact]
    public void Length_Default_IsZero()
    {
        var array = default(EquatableArray<string>);

        Assert.Equal(0, array.Length);
    }

    [Fact]
    public void AsImmutableArray_Default_ReturnsNonDefaultEmptyArray()
    {
        var array = default(EquatableArray<string>);

        var immutable = array.AsImmutableArray();

        Assert.False(immutable.IsDefault);
        Assert.Empty(immutable);
    }

    [Fact]
    public void AsImmutableArray_NonDefault_ReturnsSameElements()
    {
        var array = new[] { "a", "b" }.ToEquatableArray();

        var immutable = array.AsImmutableArray();

        Assert.Equal(new[] { "a", "b" }, immutable);
    }

    [Fact]
    public void Empty_HasZeroLengthButIsNotEqualToDefault()
    {
        // EquatableArray<T>.Empty wraps a non-default ImmutableArray<T>.Empty, whereas
        // default(EquatableArray<T>) wraps a default (IsDefault: true) ImmutableArray<T>. Equals
        // treats "default-ness" as significant, so the two are distinct despite both having
        // Length == 0.
        var empty = EquatableArray<string>.Empty;

        Assert.Equal(0, empty.Length);
        Assert.False(empty.Equals(default));
    }

    [Fact]
    public void ImplicitOperator_FromImmutableArray_PreservesElements()
    {
        ImmutableArray<string> source = ImmutableArray.Create("a", "b");

        EquatableArray<string> array = source;

        Assert.Equal(2, array.Length);
        Assert.Equal("a", array[0]);
        Assert.Equal("b", array[1]);
    }

    [Fact]
    public void Create_WrapsImmutableArrayWithEquivalentContent()
    {
        var source = ImmutableArray.Create(1, 2, 3);

        var array = EquatableArray.Create(source);

        Assert.Equal(3, array.Length);
        Assert.Equal(source, array.AsImmutableArray());
    }

    [Fact]
    public void ToEquatableArray_FromEnumerable_ProducesEquivalentArray()
    {
        IEnumerable<int> source = new List<int> { 1, 2, 3 };

        var array = source.ToEquatableArray();

        Assert.Equal(3, array.Length);
        Assert.Equal(1, array[0]);
        Assert.Equal(2, array[1]);
        Assert.Equal(3, array[2]);
    }
}
