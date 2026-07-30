namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Design doc §6 test 6 ("struct/record struct contracts"): a plain (non-record)
/// <see langword="struct"/> contract and a <see langword="readonly record struct"/> contract with
/// a primary constructor both get working generated <c>ComputeStableHash()</c> extensions, with no
/// null check (structs cannot be null) and value-equal instances producing equal hashes.
/// </summary>
public class StructRecordStructTests
{
    [Fact]
    public void PlainStructContract_EqualValues_ProduceEqualHashes()
    {
        var a = new Vector2 { X = 1, Y = 2 };
        var b = new Vector2 { X = 1, Y = 2 };

        Assert.Equal(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void PlainStructContract_DifferentValues_ProduceDifferentHashes()
    {
        var a = new Vector2 { X = 1, Y = 2 };
        var b = new Vector2 { X = 1, Y = 3 };

        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void PlainStructContract_DefaultInstance_DoesNotThrow()
    {
        // Structs cannot be null, so there is no ArgumentNullException path to exercise -- this is
        // the negative-space check for that (a default(Vector2) must hash successfully).
        Vector2 value = default;

        StableHash64 hash = value.ComputeStableHash();

        Assert.Equal(value.ComputeStableHash(), hash);
    }

    [Fact]
    public void RecordStructContract_EqualValues_ProduceEqualHashes()
    {
        var a = new Coordinate(10, 20);
        var b = new Coordinate(10, 20);
        Assert.Equal(a, b); // record struct value equality, sanity check

        Assert.Equal(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void RecordStructContract_DifferentValues_ProduceDifferentHashes()
    {
        var a = new Coordinate(10, 20);
        var b = new Coordinate(10, 21);

        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void RecordStructContract_DefaultInstance_DoesNotThrow()
    {
        Coordinate value = default;

        StableHash64 hash = value.ComputeStableHash();

        Assert.Equal(value.ComputeStableHash(), hash);
    }
}
