using SsalKit.StableHashing.Generator.Tests.TestSupport;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the generated <c>ComputeStableHash</c>/<c>AppendStableHash</c>
/// extension classes, covering the matrix that changes the emitted shape: every scalar type,
/// nullable value/reference members, all four collection forms, a nested contract member, an enum
/// member, and struct/class/record contract kinds.
/// </summary>
/// <remarks>
/// Every case also asserts the generated code actually compiles against the real
/// SsalKit.StableHashing surface before it is snapshotted, so a snapshot can never be updated to
/// something that merely looks plausible.
/// </remarks>
public class GeneratorSnapshotTests
{
    [Fact]
    public Task AllScalarTypes_GeneratesOneAppendCallPerMember()
    {
        const string source = """
            using System;
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.all-scalars", Version = 1)]
            public sealed class AllScalars
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
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task NullableMembers_AppendNullMarkerBeforeTheValue()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.nullable-members", Version = 1)]
            public sealed class NullableMembers
            {
                [StableHashMember(1)] public int? NullableInt { get; init; }
                [StableHashMember(2)] public string? NullableString { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task FourCollectionForms_UseIndexedForLoops()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.collections", Version = 1)]
            public sealed class Collections
            {
                [StableHashMember(1)] public int[] Array { get; init; } = [];
                [StableHashMember(2)] public List<int> List { get; init; } = [];
                [StableHashMember(3)] public IReadOnlyList<int> ReadOnlyList { get; init; } = [];
                [StableHashMember(4)] public ImmutableArray<int> Immutable { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task NestedCollectionOfNullableInts_RecursesThroughBothLayers()
    {
        const string source = """
            using System.Collections.Generic;
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.nested-collection", Version = 1)]
            public sealed class NestedCollection
            {
                [StableHashMember(1)] public List<int?> Values { get; init; } = [];
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task NestedContractMember_CallsTheOtherTypesAppendStableHash()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.position", Version = 1)]
            public readonly record struct Position
            {
                [StableHashMember(1)] public int X { get; init; }
                [StableHashMember(2)] public int Y { get; init; }
            }

            [StableHashContract("game.player-snapshot", Version = 1)]
            public sealed class PlayerSnapshot
            {
                [StableHashMember(1)] public Position Position { get; init; }
                [StableHashMember(2)] public Position? LastCheckpoint { get; init; }
            }
            """;

        // Two contracts declared: Position (also exercised on its own by
        // StructContract_DoesNotNullCheckTheValueParameter's sibling struct case) and
        // PlayerSnapshot, which references it. Only PlayerSnapshot's own generated file is
        // snapshotted here; both are still compiled together so PlayerSnapshot's call into
        // Position's generated AppendStableHash is verified end to end.
        var result = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly();
        var generated = result.GetSource("PlayerSnapshot.StableHash.g.cs");

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task EnumMember_CastsToTheUnderlyingTypeBeforeAppending()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            public enum Rarity : byte { Common, Rare, Legendary }

            [StableHashContract("game.item", Version = 1)]
            public sealed class Item
            {
                [StableHashMember(1)] public Rarity Rarity { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task StructContract_DoesNotNullCheckTheValueParameter()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.vector2", Version = 1)]
            public struct Vector2
            {
                [StableHashMember(1)] public int X;
                [StableHashMember(2)] public int Y;
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task RecordClassContract_NullChecksLikeAnyOtherSealedClass()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.player-name", Version = 1)]
            public sealed record PlayerName
            {
                [StableHashMember(1)] public string Value { get; init; } = "";
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task RecordStructContract_BehavesLikeAPlainStruct()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.coordinate", Version = 1)]
            public readonly record struct Coordinate(int X, int Y)
            {
                [StableHashMember(1)] public int X { get; init; } = X;
                [StableHashMember(2)] public int Y { get; init; } = Y;
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task GlobalNamespaceType_EmitsWithoutNamespaceBlock()
    {
        const string source = """
            using SsalKit.StableHashing;

            [StableHashContract("game.marker", Version = 1)]
            public sealed class Marker
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task FieldMembers_AreReadDirectlyLikeProperties()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.raw-fields", Version = 1)]
            public sealed class RawFields
            {
                [StableHashMember(1)] public int Value;
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task KeywordNamedMember_IsEscapedInTheAccessExpression()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.keyword-member", Version = 1)]
            public sealed class KeywordMember
            {
                [StableHashMember(1)] public int @class { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task NestedType_FlattensContainingTypeNamesIntoTheExtensionClassName()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            public static class Tables
            {
                [StableHashContract("game.entry", Version = 1)]
                public sealed class Entry
                {
                    [StableHashMember(1)] public int Value { get; init; }
                }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }
}
