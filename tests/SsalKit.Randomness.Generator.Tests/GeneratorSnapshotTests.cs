using SsalKit.Randomness.Generator.Tests.TestSupport;

namespace SsalKit.Randomness.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the generated extension classes, covering the matrix that changes
/// the emitted shape: weight kind (integral vs floating point), member kind (property vs field),
/// declaring-type accessibility, the <c>InternalExtensions</c> opt-out, the
/// <c>SharedSourceOverloads</c> opt-in, nested types, and the global namespace.
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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The same file, from a positional record parameter's <c>[property: RandomWeight]</c>. This is
    /// the shape the syntax-driven branch promotes to the record's synthesized property, and the
    /// snapshot is what pins that promotion producing ordinary output -- same class name, same
    /// receiver, same selector -- rather than anything that betrays where the attribute was written.
    /// </summary>
    [Fact]
    public Task PositionalRecordParameter_WithPropertyTarget_GeneratesFullSurface()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed record LootEntry(string ItemId, [property: RandomWeight] long Weight);
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The <c>SharedSourceOverloads</c> opt-in on an integral weight: seven methods, each
    /// argument-less overload sitting right after the explicit-source one it delegates to, and
    /// <c>ToWeightedSampler</c> untouched (it never took a source).
    /// </summary>
    [Fact]
    public Task LongProperty_SharedSourceOverloads_GeneratesArgumentLessOverloadsToo()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                public string ItemId { get; init; } = "";

                [RandomWeight(SharedSourceOverloads = true)]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The opt-in does not widen the floating-point matrix: a <c>double</c> weight still yields
    /// <c>PickWeighted</c> alone, now in both forms, and no <c>ToWeightedSampler</c>.
    /// </summary>
    [Fact]
    public Task DoubleProperty_SharedSourceOverloads_GeneratesBothPickWeightedFormsOnly()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight(SharedSourceOverloads = true)]
                public double Weight { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The two options are orthogonal: an internal extension class with the argument-less overloads.
    /// </summary>
    [Fact]
    public Task SharedSourceOverloadsWithInternalExtensions_CombineIndependently()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight(InternalExtensions = true, SharedSourceOverloads = true)]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// Explicitly writing the default is the same as not writing it at all: no argument-less
    /// overloads, byte-for-byte the plain <c>[RandomWeight]</c> output.
    /// </summary>
    [Fact]
    public void SharedSourceOverloadsWrittenAsFalse_MatchesTheDefaultOutput()
    {
        const string withFlag = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                public string ItemId { get; init; } = "";

                [RandomWeight(SharedSourceOverloads = false)]
                public long Weight { get; init; }
            }
            """;

        const string withoutFlag = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                public string ItemId { get; init; } = "";

                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        Assert.Equal(
            GeneratorTestSupport.RunGenerator(withoutFlag).AssertCompilesCleanlyAndGetSource(),
            GeneratorTestSupport.RunGenerator(withFlag).AssertCompilesCleanlyAndGetSource());
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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }
}
