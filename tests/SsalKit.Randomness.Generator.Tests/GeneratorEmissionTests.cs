using SsalKit.Generators.Toolkit.Testing;
using SsalKit.Randomness.Generator.Tests.TestSupport;

namespace SsalKit.Randomness.Generator.Tests;

/// <summary>
/// Assertions about individual aspects of the emitted code -- the generated method set per weight
/// type, hint names, visibility, and the delegation targets -- as opposed to
/// <see cref="GeneratorSnapshotTests"/>, which pins whole files.
/// </summary>
public class GeneratorEmissionTests
{
    private const string FullSurface = """
        PickWeighted
        PickManyWeighted
        PickManyWeightedDistinct
        ToWeightedSampler
        """;

    private static string Source(string memberDeclaration) => $$"""
        using SsalKit.Randomness;

        namespace Game.Loot;

        public sealed class LootEntry
        {
            {{memberDeclaration}}
        }
        """;

    [Theory]
    [InlineData("sbyte")]
    [InlineData("byte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    public void IntegralWeightMember_GeneratesAllFourMethods(string weightType)
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source($"[RandomWeight] public {weightType} Weight {{ get; init; }}"))
            .AssertCompilesCleanlyAndGetSource();

        foreach (var methodName in FullSurface.Split('\n').Select(name => name.Trim()))
        {
            Assert.Contains(" " + methodName + "(", generated, StringComparison.Ordinal);
        }

        Assert.Contains("static x => (long)x.Weight", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("float")]
    [InlineData("double")]
    public void FloatingWeightMember_GeneratesPickWeightedOnly(string weightType)
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source($"[RandomWeight] public {weightType} Weight {{ get; init; }}"))
            .AssertCompilesCleanlyAndGetSource();

        Assert.Contains(" PickWeighted(", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(" PickManyWeighted(", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(" PickManyWeightedDistinct(", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(" ToWeightedSampler(", generated, StringComparison.Ordinal);
        Assert.Contains("static x => (double)x.Weight", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedMethods_DelegateToTheSelectorBasedRuntimeOverloads()
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source("[RandomWeight] public long Weight { get; init; }"))
            .AssertCompilesCleanlyAndGetSource();

        Assert.Contains(
            "=> global::SsalKit.Randomness.WeightedRandomExtensions.PickWeighted(source, items, static x => (long)x.Weight);",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "=> global::SsalKit.Randomness.WeightedRandomExtensions.PickManyWeighted(source, items, static x => (long)x.Weight, count);",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "=> global::SsalKit.Randomness.WeightedRandomExtensions.PickManyWeightedDistinct(source, items, static x => (long)x.Weight, count);",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "=> global::SsalKit.Randomness.WeightedRandomExtensions.ToWeightedSampler(items, static x => (long)x.Weight);",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SharedSourceOverloads_AreNotGeneratedByDefault()
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source("[RandomWeight] public long Weight { get; init; }"))
            .AssertCompilesCleanlyAndGetSource();

        Assert.DoesNotContain("SharedRandomSource", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedSourceOverloads_DelegateToTheExplicitSourceOverloads()
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source("[RandomWeight(SharedSourceOverloads = true)] public long Weight { get; init; }"))
            .AssertCompilesCleanlyAndGetSource();

        // The argument-less forms go through the explicit-source ones rather than repeating the
        // delegation to the runtime API, so there is a single place the selector is written.
        Assert.Contains(
            "=> PickWeighted(items, global::SsalKit.Randomness.SharedRandomSource.Instance);",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "=> PickManyWeighted(items, global::SsalKit.Randomness.SharedRandomSource.Instance, count);",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "=> PickManyWeightedDistinct(items, global::SsalKit.Randomness.SharedRandomSource.Instance, count);",
            generated,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("float")]
    [InlineData("double")]
    public void SharedSourceOverloads_OnAFloatingWeight_AddPickWeightedOnly(string weightType)
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source($"[RandomWeight(SharedSourceOverloads = true)] public {weightType} Weight {{ get; init; }}"))
            .AssertCompilesCleanlyAndGetSource();

        Assert.Contains(
            "=> PickWeighted(items, global::SsalKit.Randomness.SharedRandomSource.Instance);",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" PickManyWeighted(", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(" ToWeightedSampler(", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedReceiver_IsAnIReadOnlyListOfTheDecoratedType()
    {
        var generated = GeneratorTestSupport
            .RunGenerator(Source("[RandomWeight] public long Weight { get; init; }"))
            .AssertCompilesCleanlyAndGetSource();

        Assert.Contains(
            "this global::System.Collections.Generic.IReadOnlyList<global::Game.Loot.LootEntry> items",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("global::SsalKit.Randomness.IRandomSource source", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void HintName_IsDerivedFromTheDeclaringTypesFullyQualifiedName()
    {
        var result = GeneratorTestSupport.RunGenerator(Source("[RandomWeight] public long Weight { get; init; }"));

        Assert.Equal("Game.Loot.LootEntry.RandomWeight.g.cs", result.GeneratedSources.Single().HintName);
    }

    [Fact]
    public void NestedType_HintNameFlattensThePlusSeparator()
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

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Equal("Game.Loot.Tables.Entry.RandomWeight.g.cs", result.GeneratedSources.Single().HintName);
        Assert.Contains("static class Tables_EntryRandomWeightExtensions", result.GetSingleSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void RecordDeclaringType_IsSupported()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed record LootEntry
            {
                public string ItemId { get; init; } = "";

                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        Assert.Contains("static x => (long)x.Weight", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[property: RandomWeight]")]
    [InlineData("[field: RandomWeight]")]
    public void TargetRedirectedAttribute_IsNotSeenByTheGenerator(string attribute)
    {
        // A known ForAttributeWithMetadataName limitation, pinned here so a Roslyn upgrade that
        // changes it is noticed: the provider matches an attribute against the symbol the *node*
        // declares, and a redirected target puts the attribute on a different symbol -- the
        // synthesized property of a positional record parameter, or an auto-property's backing
        // field -- which the node's own symbol does not carry. The result is silence rather than a
        // diagnostic; the workaround is to declare the weight as an ordinary property or field.
        var source = $$"""
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed record LootEntry(string ItemId, {{attribute}} long Weight);
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void StructDeclaringType_IsSupported()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public readonly struct LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        Assert.Contains("public static class LootEntryRandomWeightExtensions", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void InternalWeightMemberOnPublicType_StillYieldsPublicExtensions()
    {
        // Only the *type's* accessibility caps the generated class: the weight member is read from
        // inside the generated method body, in the same assembly, so an internal member is fine.
        var generated = GeneratorTestSupport
            .RunGenerator(Source("[RandomWeight] internal long Weight { get; init; }"))
            .AssertCompilesCleanlyAndGetSource();

        Assert.Contains("public static class LootEntryRandomWeightExtensions", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialTypeDeclaredAcrossDeclarations_ProducesASingleExtensionClass()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed partial class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }

            public sealed partial class LootEntry
            {
                public string ItemId { get; init; } = "";
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void TwoDecoratedTypes_EachGetTheirOwnFile()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }

            public sealed class MobEntry
            {
                [RandomWeight]
                public double Chance { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Equal(
            new[] { "Game.Loot.LootEntry.RandomWeight.g.cs", "Game.Loot.MobEntry.RandomWeight.g.cs" },
            result.GeneratedSources.Select(generated => generated.HintName).ToArray());
    }

    /// <summary>
    /// Flattening <c>Outer.Inner</c> to <c>Outer_Inner</c> is not injective, so a sibling type
    /// literally named <c>Tables_Entry</c> wants the same generated class name in the same
    /// namespace. Both files are still emitted (their hint names come from the fully qualified
    /// name and never collided); the second claimant's class gets a numeric suffix so the
    /// consumer's compilation does not get a CS0101.
    /// </summary>
    [Fact]
    public void FlattenedClassNameCollision_SuffixesTheSecondClaimant()
    {
        var result = GeneratorTestSupport.RunGenerator(CollidingFlattenedNames(nestedFirst: true));

        Assert.Empty(result.GetCompilationErrors());
        Assert.Equal(
            new[] { "Game.Loot.Tables.Entry.RandomWeight.g.cs", "Game.Loot.Tables_Entry.RandomWeight.g.cs" },
            result.GeneratedSources.Select(generated => generated.HintName).ToArray());

        // Ordinal order of the fully qualified names decides the winner: '.' sorts before '_', so
        // the nested Tables.Entry keeps the plain name.
        var nested = result.GetSource("Game.Loot.Tables.Entry.RandomWeight.g.cs");
        Assert.Contains("class Tables_EntryRandomWeightExtensions", nested, StringComparison.Ordinal);
        Assert.DoesNotContain("Tables_EntryRandomWeightExtensions2", nested, StringComparison.Ordinal);

        Assert.Contains(
            "class Tables_EntryRandomWeightExtensions2",
            result.GetSource("Game.Loot.Tables_Entry.RandomWeight.g.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void FlattenedClassNameCollision_DoesNotDependOnDeclarationOrder()
    {
        var nestedFirst = GeneratorTestSupport.RunGenerator(CollidingFlattenedNames(nestedFirst: true));
        var topLevelFirst = GeneratorTestSupport.RunGenerator(CollidingFlattenedNames(nestedFirst: false));

        Assert.Equal(
            nestedFirst.GeneratedSources.Select(generated => generated.HintName + "\n" + generated.Text).Order(StringComparer.Ordinal),
            topLevelFirst.GeneratedSources.Select(generated => generated.HintName + "\n" + generated.Text).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Three types in one namespace that all flatten to <c>A_B_C</c>, plus an unrelated fourth that
    /// sorts after them: each later claimant takes the next free suffix, and a type that never
    /// collided keeps its own name even though it is emitted after a renamed one.
    /// </summary>
    [Fact]
    public void FlattenedClassNameCollision_ThreeWay_TakesTheNextFreeSuffixEachTime()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public static class A
            {
                public static class B
                {
                    public sealed class C
                    {
                        [RandomWeight]
                        public long Weight { get; init; }
                    }
                }
            }

            public static class A_B
            {
                public sealed class C
                {
                    [RandomWeight]
                    public long Weight { get; init; }
                }
            }

            public sealed class A_B_C
            {
                [RandomWeight]
                public long Weight { get; init; }
            }

            public sealed class Zebra
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());

        // Ordinal fully-qualified-name order is A.B.C, A_B.C, A_B_C ('.' sorts before '_'), so the
        // suffixes land in that order and are stable against anything declared elsewhere.
        AssertGeneratedClassName(result, "Game.Loot.A.B.C.RandomWeight.g.cs", "A_B_CRandomWeightExtensions");
        AssertGeneratedClassName(result, "Game.Loot.A_B.C.RandomWeight.g.cs", "A_B_CRandomWeightExtensions2");
        AssertGeneratedClassName(result, "Game.Loot.A_B_C.RandomWeight.g.cs", "A_B_CRandomWeightExtensions3");
        AssertGeneratedClassName(result, "Game.Loot.Zebra.RandomWeight.g.cs", "ZebraRandomWeightExtensions");
    }

    private static void AssertGeneratedClassName(GeneratorTestResult result, string hintName, string expectedClassName)
    {
        var text = result.GetSource(hintName);

        // The trailing "\n" matters: without it, "...Extensions" would also match "...Extensions2".
        // IndentedCodeWriter always emits "\n", regardless of host OS.
        Assert.Contains("static class " + expectedClassName + "\n", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two decorated types in one namespace whose flattened names both come out as
    /// <c>Tables_Entry</c>, in either declaration order.
    /// </summary>
    private static string CollidingFlattenedNames(bool nestedFirst)
    {
        const string nested = """
            public static class Tables
            {
                public sealed class Entry
                {
                    [RandomWeight]
                    public long Weight { get; init; }
                }
            }
            """;

        const string topLevel = """
            public sealed class Tables_Entry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        return $"""
            using SsalKit.Randomness;

            namespace Game.Loot;

            {(nestedFirst ? nested : topLevel)}

            {(nestedFirst ? topLevel : nested)}
            """;
    }

    [Fact]
    public void NoDecoratedMember_GeneratesNothing()
    {
        const string source = """
            namespace Game.Loot;

            public sealed class LootEntry
            {
                public long Weight { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }
}
