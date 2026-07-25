using SsalKit.Randomness.Generator.Tests.TestSupport;

namespace SsalKit.Randomness.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the generated extension classes, covering the matrix that changes
/// the emitted shape: weight kind (integral vs floating point), member kind (property vs field),
/// declaring-type accessibility, the <c>InternalExtensions</c> opt-out, nested types, and the
/// global namespace.
/// </summary>
/// <remarks>
/// Every case also asserts the generated code actually compiles against the real
/// SsalKit.Randomness surface before it is snapshotted, so a snapshot can never be updated to
/// something that merely looks plausible.
/// </remarks>
public class GeneratorSnapshotTests
{
    [Fact]
    public Task LongProperty_PublicType_GeneratesFullSurface()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                public string ItemId { get; init; } = "";

                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task IntField_GeneratesFullSurfaceWithLongCast()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public int Weight;
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task DoubleProperty_GeneratesPickWeightedOnly()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public double Weight { get; init; }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task FloatField_GeneratesPickWeightedOnlyWithDoubleCast()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public float Weight;
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task InternalType_GeneratesInternalExtensions()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            internal sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task InternalExtensionsOption_ForcesInternalOnPublicType()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight(InternalExtensions = true)]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task NestedType_FlattensContainingTypeNamesIntoClassName()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public static class Tables
            {
                public sealed class Entry
                {
                    [RandomWeight]
                    public long Weight { get; init; }
                }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task GlobalNamespaceType_EmitsWithoutNamespaceBlock()
    {
        const string source = """
            using SsalKit.Randomness;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task KeywordNamedMember_IsEscapedInTheSelector()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long @long { get; init; }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).AssertCompilesCleanly();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }
}
