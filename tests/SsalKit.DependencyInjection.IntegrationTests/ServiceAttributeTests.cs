using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies the standalone behavior of <see cref="ServiceAttribute"/> itself (constructor
/// defaults, property get/set semantics, and its <see cref="AttributeUsageAttribute"/>
/// metadata), independent of the source generator.
/// </summary>
public class ServiceAttributeTests
{
    [Fact]
    public void Constructor_NoArguments_DefaultsLifetimeToSingleton()
    {
        var attribute = new ServiceAttribute();

        Assert.Equal(ServiceLifetime.Singleton, attribute.Lifetime);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void Constructor_ExplicitLifetime_IsReflectedOnLifetimeProperty(ServiceLifetime lifetime)
    {
        var attribute = new ServiceAttribute(lifetime);

        Assert.Equal(lifetime, attribute.Lifetime);
    }

    [Fact]
    public void As_DefaultsToNull()
    {
        var attribute = new ServiceAttribute();

        Assert.Null(attribute.As);
    }

    [Fact]
    public void As_CanBeSetAndReadBack()
    {
        var attribute = new ServiceAttribute
        {
            As = typeof(IDisposable),
        };

        Assert.Equal(typeof(IDisposable), attribute.As);
    }

    [Fact]
    public void Mode_DefaultsToAdd()
    {
        var attribute = new ServiceAttribute();

        Assert.Equal(RegistrationMode.Add, attribute.Mode);
        Assert.Equal(default, attribute.Mode);
    }

    [Theory]
    [InlineData(RegistrationMode.Add)]
    [InlineData(RegistrationMode.TryAdd)]
    [InlineData(RegistrationMode.TryAddEnumerable)]
    [InlineData(RegistrationMode.Replace)]
    public void Mode_CanBeSetAndReadBack(RegistrationMode mode)
    {
        var attribute = new ServiceAttribute
        {
            Mode = mode,
        };

        Assert.Equal(mode, attribute.Mode);
    }

    [Fact]
    public void Key_DefaultsToNull()
    {
        var attribute = new ServiceAttribute();

        Assert.Null(attribute.Key);
    }

    [Fact]
    public void Key_CanBeSetAndReadBack()
    {
        var attribute = new ServiceAttribute
        {
            Key = "my-key",
        };

        Assert.Equal("my-key", attribute.Key);
    }

    [Fact]
    public void AttributeUsage_AllowsMultipleOnClassOnlyAndIsNotInherited()
    {
        var usage = typeof(ServiceAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.True(usage!.AllowMultiple);
        Assert.False(usage.Inherited);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
    }
}
