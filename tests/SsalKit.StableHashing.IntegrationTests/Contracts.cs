using System.Collections.Immutable;

namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Contract types compiled directly into this test project, so
/// SsalKit.StableHashing.Generator (referenced below as an analyzer, exactly the way a NuGet
/// consumer would reference it) actually runs against them during this project's own build. This
/// is what makes the tests in this project genuine end-to-end checks of generated code, rather
/// than checks of hand-written code that merely resembles it (see
/// tests/SsalKit.StableHashing.Generator.Tests for the generator's own snapshot/diagnostic
/// coverage, which never compiles the output against the real runtime package).
/// </summary>
/// <remarks>
/// Every shape here intentionally mirrors a case from
/// SsalKit.StableHashing.Generator.Tests.GeneratorSnapshotTests, so the emitted code shape is
/// already known-good; what these tests add is exercising the *values* that flow through it.
/// </remarks>
public enum Rarity : byte
{
    Common,
    Rare,
    Legendary,
}

[StableHashContract("integration.position", Version = 1)]
public readonly record struct Position
{
    [StableHashMember(1)] public int X { get; init; }

    [StableHashMember(2)] public int Y { get; init; }
}

/// <summary>
/// The representative "comprehensive" contract: every v1 scalar type (design doc §4.4), a
/// nullable value member, a nullable reference member, all four supported collection forms, a
/// nested <see cref="Position"/> contract member, and an <see langword="enum"/> member. Used by
/// the generated-vs-manual-writer cross-check, the golden vector, distinguishability, and
/// null-handling tests.
/// </summary>
[StableHashContract("integration.comprehensive", Version = 1)]
public sealed record ComprehensiveContract
{
    [StableHashMember(1)] public bool Bool { get; init; }

    [StableHashMember(2)] public char Char { get; init; }

    [StableHashMember(3)] public sbyte SByte { get; init; }

    [StableHashMember(4)] public byte Byte { get; init; }

    [StableHashMember(5)] public short Int16 { get; init; }

    [StableHashMember(6)] public ushort UInt16 { get; init; }

    [StableHashMember(7)] public int Int32 { get; init; }

    [StableHashMember(8)] public uint UInt32 { get; init; }

    [StableHashMember(9)] public long Int64 { get; init; }

    [StableHashMember(10)] public ulong UInt64 { get; init; }

    [StableHashMember(11)] public Int128 Int128 { get; init; }

    [StableHashMember(12)] public UInt128 UInt128 { get; init; }

    [StableHashMember(13)] public float Single { get; init; }

    [StableHashMember(14)] public double Double { get; init; }

    [StableHashMember(15)] public decimal Decimal { get; init; }

    [StableHashMember(16)] public string String { get; init; } = "";

    [StableHashMember(17)] public Guid Guid { get; init; }

    [StableHashMember(18)] public DateOnly DateOnly { get; init; }

    [StableHashMember(19)] public TimeOnly TimeOnly { get; init; }

    [StableHashMember(20)] public TimeSpan TimeSpan { get; init; }

    [StableHashMember(21)] public DateTimeOffset DateTimeOffset { get; init; }

    [StableHashMember(22)] public int? NullableInt { get; init; }

    [StableHashMember(23)] public string? NullableString { get; init; }

    [StableHashMember(24)] public int[] Array { get; init; } = [];

    [StableHashMember(25)] public List<int> List { get; init; } = [];

    [StableHashMember(26)] public IReadOnlyList<int> ReadOnlyList { get; init; } = [];

    [StableHashMember(27)] public ImmutableArray<int> Immutable { get; init; }

    [StableHashMember(28)] public Position NestedPosition { get; init; }

    [StableHashMember(29)] public Rarity Rarity { get; init; }
}

/// <summary>A plain (non-record) struct contract -- exercises §4.5's "struct contracts are
/// inherently safe, so sealed is not required" path end to end.</summary>
[StableHashContract("integration.vector2", Version = 1)]
public struct Vector2
{
    [StableHashMember(1)] public int X;

    [StableHashMember(2)] public int Y;
}

/// <summary>A <see langword="readonly record struct"/> contract with a primary constructor.</summary>
[StableHashContract("integration.coordinate", Version = 1)]
public readonly record struct Coordinate(int X, int Y)
{
    [StableHashMember(1)] public int X { get; init; } = X;

    [StableHashMember(2)] public int Y { get; init; } = Y;
}

/// <summary>A sealed record class contract -- exercises the class null-check path (design §3.4)
/// on a record rather than a plain class.</summary>
[StableHashContract("integration.player-name", Version = 1)]
public sealed record PlayerName
{
    [StableHashMember(1)] public string Value { get; init; } = "";
}
