using System.Reflection;
using SsalKit.Randomness.IntegrationTests.TestModels;

namespace SsalKit.Randomness.IntegrationTests;

/// <summary>
/// Inspects the generated types themselves -- which methods exist and how visible the extension
/// class is -- rather than what they return.
/// </summary>
/// <remarks>
/// Reflection is the only way to assert that something was <em>not</em> generated: a missing method
/// cannot be written down in source without failing to compile.
/// </remarks>
public class GeneratedSurfaceTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

    private static readonly string[] IntegralOnlyMethods = ["PickManyWeighted", "PickManyWeightedDistinct", "ToWeightedSampler"];

    [Fact]
    public void IntegralWeight_GeneratesTheFullPickingSurface()
    {
        var extensions = typeof(LootEntryRandomWeightExtensions);

        Assert.NotNull(extensions.GetMethod("PickWeighted", PublicStatic));
        foreach (var name in IntegralOnlyMethods)
        {
            Assert.NotNull(extensions.GetMethod(name, PublicStatic));
        }
    }

    /// <summary>
    /// A <see cref="double"/> weight mirrors the runtime surface exactly, and the runtime has no
    /// batched or alias-table API for <see cref="double"/> weights -- so only <c>PickWeighted</c> is
    /// generated. This is documented behaviour, not a diagnostic.
    /// </summary>
    [Fact]
    public void DoubleWeight_GeneratesPickWeightedOnly()
    {
        var extensions = typeof(DoubleWeightedItemRandomWeightExtensions);

        Assert.NotNull(extensions.GetMethod("PickWeighted", PublicStatic));
        foreach (var name in IntegralOnlyMethods)
        {
            Assert.Null(extensions.GetMethod(name, PublicStatic));
        }
    }

    [Fact]
    public void FloatWeight_GeneratesPickWeightedOnly()
    {
        var extensions = typeof(FloatWeightedItemRandomWeightExtensions);

        Assert.NotNull(extensions.GetMethod("PickWeighted", PublicStatic));
        foreach (var name in IntegralOnlyMethods)
        {
            Assert.Null(extensions.GetMethod(name, PublicStatic));
        }
    }

    [Fact]
    public void PublicType_GeneratesPublicExtensionClass()
    {
        Assert.True(typeof(LootEntryRandomWeightExtensions).IsPublic);
        Assert.True(typeof(LootEntryRandomWeightExtensions).IsAbstract);
        Assert.True(typeof(LootEntryRandomWeightExtensions).IsSealed);
    }

    [Fact]
    public void InternalType_GeneratesInternalExtensionClass()
    {
        Assert.False(typeof(InternalWeightedItemRandomWeightExtensions).IsPublic);
        Assert.True(typeof(InternalWeightedItemRandomWeightExtensions).IsNotPublic);
    }

    [Fact]
    public void InternalExtensionsOption_ForcesInternalExtensionClass_OnAPublicType()
    {
        Assert.True(typeof(ForcedInternalItem).IsPublic);
        Assert.False(typeof(ForcedInternalItemRandomWeightExtensions).IsPublic);
        Assert.True(typeof(ForcedInternalItemRandomWeightExtensions).IsNotPublic);
    }

    /// <summary>
    /// A nested type's extension class is a top-level class in the same namespace, with the
    /// containing type's name flattened into its own.
    /// </summary>
    [Fact]
    public void NestedType_GeneratesFlattenedTopLevelExtensionClass()
    {
        var extensions = typeof(WeightedContainer_NestedItemRandomWeightExtensions);

        Assert.Null(extensions.DeclaringType);
        Assert.Equal(typeof(WeightedContainer.NestedItem).Namespace, extensions.Namespace);
        Assert.True(extensions.IsPublic);
    }

    /// <summary>
    /// The generated extensions really are extension methods (usable with instance-call syntax),
    /// which the parity tests already rely on but only implicitly.
    /// </summary>
    [Fact]
    public void GeneratedMethods_AreExtensionMethods_TakingTheSourceExplicitly()
    {
        var pick = typeof(LootEntryRandomWeightExtensions).GetMethod("PickWeighted", PublicStatic);

        Assert.NotNull(pick);
        Assert.True(pick.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), inherit: false));

        var parameters = pick.GetParameters();
        Assert.Equal(typeof(IReadOnlyList<LootEntry>), parameters[0].ParameterType);
        Assert.Equal(typeof(IRandomSource), parameters[1].ParameterType);
        Assert.Equal(typeof(LootEntry), pick.ReturnType);
    }
}
