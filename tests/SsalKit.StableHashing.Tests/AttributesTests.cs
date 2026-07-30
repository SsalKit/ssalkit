namespace SsalKit.StableHashing.Tests;

/// <summary>
/// Basic property coverage for <see cref="StableHashContractAttribute"/> and
/// <see cref="StableHashMemberAttribute"/>. These attributes carry no behavior of their own in
/// this runtime-only stage (SsalKit.StableHashing.Generator, which reads them, is a separate,
/// not-yet-shipped package) — these tests just pin the constructor/property contract the future
/// generator will rely on.
/// </summary>
public class AttributesTests
{
    [Fact]
    public void StableHashContractAttribute_Name_IsSetFromConstructor()
    {
        var attribute = new StableHashContractAttribute("game.player-snapshot");

        Assert.Equal("game.player-snapshot", attribute.Name);
    }

    [Fact]
    public void StableHashContractAttribute_Version_DefaultsToOne()
    {
        var attribute = new StableHashContractAttribute("game.player-snapshot");

        Assert.Equal(1, attribute.Version);
    }

    [Fact]
    public void StableHashContractAttribute_Version_CanBeSetExplicitly()
    {
        var attribute = new StableHashContractAttribute("game.player-snapshot") { Version = 3 };

        Assert.Equal(3, attribute.Version);
    }

    [Fact]
    public void StableHashContractAttribute_HasExpectedAttributeUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(StableHashContractAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void StableHashMemberAttribute_Id_IsSetFromConstructor()
    {
        var attribute = new StableHashMemberAttribute(7);

        Assert.Equal(7, attribute.Id);
    }

    [Fact]
    public void StableHashMemberAttribute_HasExpectedAttributeUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(StableHashMemberAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
