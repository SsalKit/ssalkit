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
