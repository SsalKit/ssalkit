using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies how the generator decides which type(s) a class is registered as: the single
/// directly-implemented interface when <c>As</c> is omitted, the class itself when it implements
/// no interface, or exactly the type named by <c>As</c> when specified.
/// </summary>
public class ServiceTypeResolutionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void NoAs_SingleInterface_ResolvesThroughInterface_NotThroughConcreteType()
    {
        using var provider = BuildProvider();

        var viaInterface = provider.GetService<ISingletonMarkerService>();
        var viaConcrete = provider.GetService<SingletonMarkerService>();

        Assert.NotNull(viaInterface);
        Assert.Null(viaConcrete);
    }

    [Fact]
    public void NoAs_NoInterface_ResolvesAsSelf()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<SelfRegisteredService>();
        var second = provider.GetRequiredService<SelfRegisteredService>();

        Assert.NotSame(first, second); // Transient
        Assert.NotEqual(Guid.Empty, first.Id);
    }

    [Fact]
    public void As_Specified_RegistersOnlyThatType()
    {
        using var provider = BuildProvider();

        var viaPrimary = provider.GetService<IPrimaryContract>();
        var viaSecondary = provider.GetService<ISecondaryContract>();
        var viaConcrete = provider.GetService<AsSpecifiedService>();

        Assert.NotNull(viaPrimary);
        Assert.IsType<AsSpecifiedService>(viaPrimary);
        Assert.Null(viaSecondary);
        Assert.Null(viaConcrete);
    }
}
