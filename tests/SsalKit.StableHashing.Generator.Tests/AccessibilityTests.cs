using Microsoft.CodeAnalysis;
using SsalKit.StableHashing.Generator.Tests.TestSupport;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// Verifies that the generated extension class's accessibility always matches the contract type's
/// own effective accessibility (public only when the type and every type containing it are
/// public), never hard-coded to <see langword="public"/>. Getting this wrong is not just a
/// visibility leak: a <see langword="public"/> extension method whose <c>this</c> parameter type
/// is only <see langword="internal"/> is CS0051 ("inconsistent accessibility"), so an internal
/// contract with a hard-coded-public emitter would simply fail to compile.
/// </summary>
public class AccessibilityTests
{
    [Fact]
    public void PublicTopLevelContract_YieldsAPublicExtensionClass()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.public-contract", Version = 1)]
            public sealed class PublicContract
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        AssertExtensionClassAccessibility(result, "Game.Snapshots.PublicContractStableHashing", Accessibility.Public);
    }

    [Fact]
    public void InternalTopLevelContract_YieldsAnInternalExtensionClass()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.internal-contract", Version = 1)]
            internal sealed class InternalContract
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        AssertExtensionClassAccessibility(result, "Game.Snapshots.InternalContractStableHashing", Accessibility.Internal);
    }

    /// <summary>
    /// The reviewer-flagged case: a <see langword="public"/>-declared contract type nested inside
    /// an <see langword="internal"/> container is not effectively public (nothing outside the
    /// assembly can name it, regardless of its own declared accessibility), so the generated
    /// extension class must be downgraded to <see langword="internal"/> too -- not left
    /// <see langword="public"/> just because the contract type's own modifier says so.
    /// </summary>
    [Fact]
    public void PublicContractNestedInsideAnInternalContainer_DowngradesToInternal()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            internal static class Container
            {
                [StableHashContract("game.nested-in-internal", Version = 1)]
                public sealed class Nested
                {
                    [StableHashMember(1)] public int Value { get; init; }
                }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        AssertExtensionClassAccessibility(result, "Game.Snapshots.Container_NestedStableHashing", Accessibility.Internal);
    }

    /// <summary>
    /// <c>protected internal</c> is, in effect, at least internal (it grants internal access on top
    /// of protected), so a top-level type cannot actually declare it -- this exercises the
    /// nested-in-a-<c>protected internal</c>-container case instead, via a public outer class with
    /// a <c>protected internal</c> nested container.
    /// </summary>
    [Fact]
    public void ContractNestedInsideAProtectedInternalContainer_DowngradesToInternal()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            public class Outer
            {
                protected internal static class Container
                {
                    [StableHashContract("game.nested-in-protected-internal", Version = 1)]
                    public sealed class Nested
                    {
                        [StableHashMember(1)] public int Value { get; init; }
                    }
                }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        AssertExtensionClassAccessibility(result, "Game.Snapshots.Outer_Container_NestedStableHashing", Accessibility.Internal);
    }

    private static void AssertExtensionClassAccessibility(
        SsalKit.Generators.Toolkit.Testing.GeneratorTestResult result, string metadataName, Accessibility expected)
    {
        var extensionClass = result.OutputCompilation.GetTypeByMetadataName(metadataName);

        Assert.NotNull(extensionClass);
        Assert.Equal(expected, extensionClass!.DeclaredAccessibility);
    }
}
