using System;
using SsalKit.StableHashing.Generator.Tests.TestSupport;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// The reviewer's reproduction: a nested <c>Outer.Inner</c> contract and an unrelated top-level
/// <c>Outer_Inner</c> contract, in the same namespace, both flatten to the same generated class
/// name (<c>ContractNaming.BuildExtensionClassName</c> joins a nesting chain with <c>_</c>). Ported
/// from <c>SsalKit.Randomness.Generator</c>'s own disambiguation tests for
/// <c>RandomWeightTypeGrouper</c>.
/// </summary>
public class ExtensionClassNameCollisionTests
{
    private const string CollidingContracts = """
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        public static class Outer
        {
            [StableHashContract("game.outer-inner-nested", Version = 1)]
            public sealed class Inner
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
        }

        [StableHashContract("game.outer-inner-top-level", Version = 1)]
        public sealed class Outer_Inner
        {
            [StableHashMember(1)] public int Value { get; init; }
        }
        """;

    /// <summary>
    /// Both types must compile with two *distinct* class names -- if disambiguation were missing,
    /// this would be CS0101 ("the namespace already contains a definition").
    /// </summary>
    [Fact]
    public void BothCollidingContracts_CompileWithDistinctClassNames()
    {
        var result = GeneratorTestSupport.RunGenerator(CollidingContracts);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GetCompilationErrors());
        Assert.Equal(2, result.GeneratedSources.Length);

        var nestedClass = result.OutputCompilation.GetTypeByMetadataName("Game.Snapshots.Outer_InnerStableHashing");
        var topLevelClass = result.OutputCompilation.GetTypeByMetadataName("Game.Snapshots.Outer_InnerStableHashing2");

        Assert.NotNull(nestedClass);
        Assert.NotNull(topLevelClass);
        Assert.NotEqual(nestedClass, topLevelClass);
    }

    /// <summary>
    /// The winner of the plain (unsuffixed) name is decided by <c>TypeFqn</c> in ordinal order, not
    /// by which type happens to be declared or processed first: <c>"Game.Snapshots.Outer.Inner"</c>
    /// sorts before <c>"Game.Snapshots.Outer_Inner"</c> ordinally (<c>'.'</c> is 0x2E, <c>'_'</c> is
    /// 0x5F), so the nested <c>Outer.Inner</c> keeps the plain <c>Outer_InnerStableHashing</c> name
    /// and the top-level <c>Outer_Inner</c> is the one that gets suffixed.
    /// </summary>
    [Fact]
    public void TheOrdinallyFirstTypeFqn_KeepsThePlainName()
    {
        var result = GeneratorTestSupport.RunGenerator(CollidingContracts);

        var nestedSource = result.GetSource("Game.Snapshots.Outer.Inner.StableHash.g.cs");
        var topLevelSource = result.GetSource("Game.Snapshots.Outer_Inner.StableHash.g.cs");

        Assert.Contains("static class Outer_InnerStableHashing\n", nestedSource, StringComparison.Ordinal);
        Assert.Contains("static class Outer_InnerStableHashing2\n", topLevelSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// A third contract referencing *either* colliding type as a nested member must call into that
    /// type's actual, final (possibly suffixed) generated class -- not the tentative,
    /// pre-disambiguation name computed before every contract in the compilation was known. This is
    /// the propagation half of the fix: without it, a reference to the type that lost the plain
    /// name would call a class that does not exist under that name (CS0103).
    /// </summary>
    [Fact]
    public void ReferencingEitherCollidingContractAsAMember_CallsItsActualGeneratedClass()
    {
        const string source = CollidingContracts + """


            [StableHashContract("game.wraps-nested", Version = 1)]
            public sealed class WrapsNested
            {
                [StableHashMember(1)] public Outer.Inner Value { get; init; } = null!;
            }

            [StableHashContract("game.wraps-top-level", Version = 1)]
            public sealed class WrapsTopLevel
            {
                [StableHashMember(1)] public Outer_Inner Value { get; init; } = null!;
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GetCompilationErrors());

        var wrapsNestedSource = result.GetSource("Game.Snapshots.WrapsNested.StableHash.g.cs");
        var wrapsTopLevelSource = result.GetSource("Game.Snapshots.WrapsTopLevel.StableHash.g.cs");

        // WrapsNested holds Outer.Inner, which kept the plain name.
        Assert.Contains(
            "global::Game.Snapshots.Outer_InnerStableHashing.AppendStableHash(", wrapsNestedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Outer_InnerStableHashing2", wrapsNestedSource, StringComparison.Ordinal);

        // WrapsTopLevel holds the top-level Outer_Inner, which was the one suffixed.
        Assert.Contains(
            "global::Game.Snapshots.Outer_InnerStableHashing2.AppendStableHash(", wrapsTopLevelSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// The outcome must not depend on the order the two colliding contracts happen to appear in
    /// source: reversing their declaration order still leaves <c>Outer.Inner</c> (the ordinally
    /// first <c>TypeFqn</c>) holding the plain name.
    /// </summary>
    [Fact]
    public void SuffixAssignment_IsIndependentOfDeclarationOrder()
    {
        const string reordered = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.outer-inner-top-level", Version = 1)]
            public sealed class Outer_Inner
            {
                [StableHashMember(1)] public int Value { get; init; }
            }

            public static class Outer
            {
                [StableHashContract("game.outer-inner-nested", Version = 1)]
                public sealed class Inner
                {
                    [StableHashMember(1)] public int Value { get; init; }
                }
            }
            """;

        var original = GeneratorTestSupport.RunGenerator(CollidingContracts);
        var swapped = GeneratorTestSupport.RunGenerator(reordered);

        var originalNested = original.GetSource("Game.Snapshots.Outer.Inner.StableHash.g.cs");
        var swappedNested = swapped.GetSource("Game.Snapshots.Outer.Inner.StableHash.g.cs");

        Assert.Contains("static class Outer_InnerStableHashing\n", originalNested, StringComparison.Ordinal);
        Assert.Contains("static class Outer_InnerStableHashing\n", swappedNested, StringComparison.Ordinal);

        var originalTopLevel = original.GetSource("Game.Snapshots.Outer_Inner.StableHash.g.cs");
        var swappedTopLevel = swapped.GetSource("Game.Snapshots.Outer_Inner.StableHash.g.cs");

        Assert.Contains("static class Outer_InnerStableHashing2\n", originalTopLevel, StringComparison.Ordinal);
        Assert.Contains("static class Outer_InnerStableHashing2\n", swappedTopLevel, StringComparison.Ordinal);
    }
}
