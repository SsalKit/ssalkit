using System.Reflection;

namespace SsalKit.Randomness.Tests;

/// <summary>
/// Covers <see cref="RandomWeightAttribute"/>: its declared usage contract (the targets it may be
/// applied to, non-multiple, non-inherited) and the <see cref="RandomWeightAttribute.InternalExtensions"/>
/// property. The attribute carries no runtime behaviour, so what is worth pinning is the shape the
/// source generator and consumers rely on.
/// </summary>
public class RandomWeightAttributeTests
{
    private sealed class Decorated
    {
        [RandomWeight]
        public long WeightProperty { get; init; }

        [RandomWeight(InternalExtensions = true)]
        public int WeightField = 1;
    }

    [Fact]
    public void AttributeUsage_TargetsPropertiesAndFieldsOnly()
    {
        var usage = typeof(RandomWeightAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
    }

    [Fact]
    public void AttributeUsage_DisallowsMultipleAndIsNotInherited()
    {
        var usage = typeof(RandomWeightAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void Attribute_IsSealed()
    {
        Assert.True(typeof(RandomWeightAttribute).IsSealed);
    }

    [Fact]
    public void InternalExtensions_DefaultsToFalse()
    {
        var attribute = new RandomWeightAttribute();

        Assert.False(attribute.InternalExtensions);
    }

    [Fact]
    public void InternalExtensions_RoundTripsWhenSet()
    {
        var attribute = new RandomWeightAttribute { InternalExtensions = true };

        Assert.True(attribute.InternalExtensions);

        attribute.InternalExtensions = false;

        Assert.False(attribute.InternalExtensions);
    }

    [Fact]
    public void Attribute_OnProperty_IsDiscoverableWithDefaultInternalExtensions()
    {
        PropertyInfo property = typeof(Decorated).GetProperty(nameof(Decorated.WeightProperty))!;
        var attribute = property.GetCustomAttribute<RandomWeightAttribute>();

        Assert.NotNull(attribute);
        Assert.False(attribute.InternalExtensions);
    }

    [Fact]
    public void Attribute_OnField_CarriesNamedArgument()
    {
        FieldInfo field = typeof(Decorated).GetField(nameof(Decorated.WeightField))!;
        var attribute = field.GetCustomAttribute<RandomWeightAttribute>();

        Assert.NotNull(attribute);
        Assert.True(attribute.InternalExtensions);
    }
}
