using Microsoft.CodeAnalysis;
using SsalKit.Randomness.Generator.Tests.TestSupport;

namespace SsalKit.Randomness.Generator.Tests;

/// <summary>
/// Compiles call sites written against the generated API in the same compilation the generator
/// runs on. Unlike the snapshot tests -- which only prove the generated file itself type-checks --
/// these prove the emitted signatures are actually reachable and bindable the way the design
/// promises: collection receiver, explicit random source, no <c>using</c> beyond the model's own
/// namespace.
/// </summary>
public class GeneratedApiUsabilityTests
{
    [Fact]
    public void IntegralWeight_AllFourGeneratedMethodsBindFromACallSite()
    {
        const string source = """
            using System.Collections.Generic;
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                public string ItemId { get; init; } = "";

                [RandomWeight]
                public long Weight { get; init; }
            }

            public static class Consumer
            {
                public static void Use(IReadOnlyList<LootEntry> table)
                {
                    IRandomSource random = new DeterministicRandom(42);

                    LootEntry single = table.PickWeighted(random);
                    LootEntry[] many = table.PickManyWeighted(random, 3);
                    LootEntry[] distinct = table.PickManyWeightedDistinct(random, 2);
                    WeightedSampler<LootEntry> sampler = table.ToWeightedSampler();
                    LootEntry fromSampler = sampler.Pick(random);

                    _ = (single, many, distinct, fromSampler);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.SsalrDiagnostics);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void ArrayAndListReceivers_BothBind()
    {
        const string source = """
            using System.Collections.Generic;
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public int Weight { get; init; }
            }

            public static class Consumer
            {
                public static void Use(LootEntry[] array, List<LootEntry> list)
                {
                    IRandomSource random = new DeterministicRandom(1);

                    _ = array.PickWeighted(random);
                    _ = list.PickWeighted(random);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void FloatingWeight_PickManyIsAbsentRatherThanBroken()
    {
        // A double weight member yields PickWeighted only, mirroring the runtime surface. The
        // design calls for the batched overloads to simply not exist -- which shows up as an
        // ordinary "no such method" error at the call site, not as a generator diagnostic.
        const string source = """
            using System.Collections.Generic;
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public double Weight { get; init; }
            }

            public static class Consumer
            {
                public static void Use(IReadOnlyList<LootEntry> table)
                {
                    IRandomSource random = new DeterministicRandom(1);

                    _ = table.PickWeighted(random);
                    _ = table.PickManyWeighted(random, 2);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.SsalrDiagnostics);

        var error = Assert.Single(result.GetOutputCompilationErrors());
        Assert.Contains("PickManyWeighted", error.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void InternalExtensionsOption_KeepsTheHelpersOutOfThePublicApi()
    {
        const string source = """
            using System.Collections.Generic;
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight(InternalExtensions = true)]
                public long Weight { get; init; }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GetOutputCompilationErrors());

        var extensionClass = result.OutputCompilation.GetTypeByMetadataName("Game.Loot.LootEntryRandomWeightExtensions");
        Assert.NotNull(extensionClass);
        Assert.Equal(Accessibility.Internal, extensionClass!.DeclaredAccessibility);
    }

    [Fact]
    public void PublicType_YieldsAPublicExtensionClass()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        var extensionClass = result.OutputCompilation.GetTypeByMetadataName("Game.Loot.LootEntryRandomWeightExtensions");
        Assert.NotNull(extensionClass);
        Assert.Equal(Accessibility.Public, extensionClass!.DeclaredAccessibility);
    }

    [Fact]
    public void InternalType_YieldsAnInternalExtensionClass()
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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GetOutputCompilationErrors());

        var extensionClass = result.OutputCompilation.GetTypeByMetadataName("Game.Loot.LootEntryRandomWeightExtensions");
        Assert.NotNull(extensionClass);
        Assert.Equal(Accessibility.Internal, extensionClass!.DeclaredAccessibility);
    }

    [Fact]
    public void NestedType_ExtensionsBindFromACallSite()
    {
        const string source = """
            using System.Collections.Generic;
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

            public static class Consumer
            {
                public static Tables.Entry Use(IReadOnlyList<Tables.Entry> table) =>
                    table.PickWeighted(new DeterministicRandom(7));
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void GlobalNamespaceType_ExtensionsBindFromACallSite()
    {
        const string source = """
            using System.Collections.Generic;
            using SsalKit.Randomness;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }

            public static class Consumer
            {
                public static LootEntry Use(IReadOnlyList<LootEntry> table) =>
                    table.PickWeighted(new DeterministicRandom(7));
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GetOutputCompilationErrors());
    }
}
