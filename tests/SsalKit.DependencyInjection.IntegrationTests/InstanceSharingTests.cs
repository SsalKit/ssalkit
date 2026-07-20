using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies that when a class implements 2+ interfaces with no explicit <c>As</c>, a
/// Singleton/Scoped lifetime shares a single underlying instance across every forwarded
/// interface and the concrete type itself.
/// </summary>
public class InstanceSharingTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Singleton_MultiInterface_AllResolutionsShareSameInstance()
    {
        using var provider = BuildProvider();

        var reader = provider.GetRequiredService<IReaderContract>();
        var writer = provider.GetRequiredService<IWriterContract>();
        var concrete = provider.GetRequiredService<MultiInterfaceSingletonService>();

        Assert.Same(reader, writer);
        Assert.Same(reader, concrete);
    }

    [Fact]
    public void Scoped_MultiInterface_ShareSameInstanceWithinScope_ButNotAcrossScopes()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        var readerA = scopeA.ServiceProvider.GetRequiredService<IScopedReaderContract>();
        var writerA = scopeA.ServiceProvider.GetRequiredService<IScopedWriterContract>();
        var concreteA = scopeA.ServiceProvider.GetRequiredService<MultiInterfaceScopedService>();

        using var scopeB = provider.CreateScope();
        var readerB = scopeB.ServiceProvider.GetRequiredService<IScopedReaderContract>();

        Assert.Same(readerA, writerA);
        Assert.Same(readerA, concreteA);
        Assert.NotSame(readerA, readerB);
    }
}
