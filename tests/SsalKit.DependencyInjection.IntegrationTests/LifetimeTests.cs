using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies that the lifetime specified on <c>[Service]</c> (Singleton / Scoped / Transient) is
/// honored at runtime by the generated registration.
/// </summary>
public class LifetimeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Singleton_ResolvesSameInstance_AcrossMultipleResolutionsAndScopes()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ISingletonMarkerService>();
        var second = provider.GetRequiredService<ISingletonMarkerService>();

        using var scope = provider.CreateScope();
        var fromScope = scope.ServiceProvider.GetRequiredService<ISingletonMarkerService>();

        Assert.Same(first, second);
        Assert.Same(first, fromScope);
    }

    [Fact]
    public void Scoped_ResolvesSameInstance_WithinSameScope()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IScopedMarkerService>();
        var second = scope.ServiceProvider.GetRequiredService<IScopedMarkerService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Scoped_ResolvesDifferentInstance_AcrossDifferentScopes()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var fromA = scopeA.ServiceProvider.GetRequiredService<IScopedMarkerService>();
        var fromB = scopeB.ServiceProvider.GetRequiredService<IScopedMarkerService>();

        Assert.NotSame(fromA, fromB);
    }

    [Fact]
    public void Transient_ResolvesNewInstance_EveryTime()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ITransientMarkerService>();
        var second = provider.GetRequiredService<ITransientMarkerService>();

        Assert.NotSame(first, second);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Default_NoLifetimeSpecified_ResolvesAsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IDefaultLifetimeMarkerService>();
        var second = provider.GetRequiredService<IDefaultLifetimeMarkerService>();

        using var scope = provider.CreateScope();
        var fromScope = scope.ServiceProvider.GetRequiredService<IDefaultLifetimeMarkerService>();

        Assert.Same(first, second);
        Assert.Same(first, fromScope);
    }
}
