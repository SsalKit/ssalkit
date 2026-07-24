using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies that <c>[Service(Factory = "...")]</c> actually invokes the named factory method to
/// construct the implementation instance, instead of the container attempting constructor
/// activation. Every test service in <c>TestServices/FactoryServices.cs</c> has a private
/// constructor, so a successful resolution is itself proof the factory ran.
/// </summary>
public class FactoryTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ParameterlessFactory_IsInvoked_ProducesInstanceFromFactory()
    {
        using var provider = BuildProvider();

        var resolved = provider.GetRequiredService<IParameterlessFactoryContract>();

        Assert.Equal("created-by-parameterless-factory", resolved.Marker);
    }

    [Fact]
    public void ServiceProviderFactory_IsInvokedWithRealServiceProvider_CanResolveDependencies()
    {
        using var provider = BuildProvider();

        var resolved = provider.GetRequiredService<IServiceProviderFactoryContract>();
        var expectedDependency = provider.GetRequiredService<IParameterlessFactoryContract>();

        Assert.Same(expectedDependency, resolved.Dependency);
    }

    [Fact]
    public void Singleton_FactoryInvokedOnce_SameInstanceEverywhere()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IFactorySingletonContract>();
        var second = provider.GetRequiredService<IFactorySingletonContract>();

        using var scope = provider.CreateScope();
        var fromScope = scope.ServiceProvider.GetRequiredService<IFactorySingletonContract>();

        Assert.Same(first, second);
        Assert.Same(first, fromScope);
    }

    [Fact]
    public void Scoped_FactoryInvokedOncePerScope()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        var firstInA = scopeA.ServiceProvider.GetRequiredService<IFactoryScopedContract>();
        var secondInA = scopeA.ServiceProvider.GetRequiredService<IFactoryScopedContract>();

        using var scopeB = provider.CreateScope();
        var fromB = scopeB.ServiceProvider.GetRequiredService<IFactoryScopedContract>();

        Assert.Same(firstInA, secondInA);
        Assert.NotSame(firstInA, fromB);
    }

    [Fact]
    public void Transient_FactoryInvokedEveryResolution()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IFactoryTransientContract>();
        var second = provider.GetRequiredService<IFactoryTransientContract>();

        Assert.NotSame(first, second);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Keyed_FactoryResolution_EachKeyInvokesItsOwnFactory()
    {
        using var provider = BuildProvider();

        var alpha = provider.GetRequiredKeyedService<IKeyedFactoryContract>("alpha");
        var beta = provider.GetRequiredKeyedService<IKeyedFactoryContract>("beta");

        Assert.Equal("alpha", alpha.Origin);
        Assert.Equal("beta", beta.Origin);
    }

    [Fact]
    public void MultiInterfaceForwarding_SharesSingleFactoryCreatedInstance()
    {
        using var provider = BuildProvider();

        var reader = provider.GetRequiredService<IMultiInterfaceFactoryReaderContract>();
        var writer = provider.GetRequiredService<IMultiInterfaceFactoryWriterContract>();
        var concrete = provider.GetRequiredService<MultiInterfaceFactoryService>();

        Assert.Same(reader, writer);
        Assert.Same(reader, concrete);
    }

    [Fact]
    public void TryAddEnumerable_WithFactory_SuppressesDuplicateAgainstExistingDescriptor()
    {
        // A manually-registered descriptor for the exact same (ServiceType, ImplementationType)
        // pair the generator would also emit -- TryAddEnumerable must still recognize the
        // generated factory-backed descriptor as a duplicate (its ImplementationType is preserved
        // by the two-generic-argument ServiceDescriptor.Singleton<TService, TImplementation>
        // overload) and skip adding a second entry.
        var services = new ServiceCollection();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITryAddEnumerableFactoryContract, TryAddEnumerableFactoryService>(
                _ => TryAddEnumerableFactoryService.Create()));

        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var all = provider.GetServices<ITryAddEnumerableFactoryContract>().ToList();

        Assert.Single(all);
    }
}
