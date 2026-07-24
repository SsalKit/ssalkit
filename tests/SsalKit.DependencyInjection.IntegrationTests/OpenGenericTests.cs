using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// End-to-end tests for open generic <c>[Service]</c> registrations, run through the real
/// generator (see the project's Analyzer reference), verifying MEDI actually resolves the
/// generated <c>typeof(...)</c>-based registrations as expected at runtime.
/// </summary>
public class OpenGenericTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Singleton_SameClosedServiceType_ResolvesSameInstance()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IOpenGenericRepository<int>>();
        var second = provider.GetRequiredService<IOpenGenericRepository<int>>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Singleton_DifferentClosedServiceTypes_ResolveDifferentInstances()
    {
        using var provider = BuildProvider();

        var intRepo = provider.GetRequiredService<IOpenGenericRepository<int>>();
        var stringRepo = provider.GetRequiredService<IOpenGenericRepository<string>>();

        Assert.NotSame(intRepo, stringRepo);
    }

    [Fact]
    public void Transient_ResolvesNewInstance_EveryTime()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ITransientOpenGenericRepository<int>>();
        var second = provider.GetRequiredService<ITransientOpenGenericRepository<int>>();

        Assert.NotSame(first, second);
        Assert.NotEqual(first.InstanceId, second.InstanceId);
    }

    [Fact]
    public void Arity2Service_ResolvesSuccessfully()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IOpenGenericPair<int, string>>();
        var second = provider.GetRequiredService<IOpenGenericPair<int, string>>();
        var differentClosure = provider.GetRequiredService<IOpenGenericPair<string, int>>();

        Assert.IsType<OpenGenericPair<int, string>>(first);
        Assert.Same(first, second);
        Assert.NotSame(first, differentClosure);
    }

    [Fact]
    public void KeyedOpenGeneric_ResolvesThroughKey()
    {
        using var provider = BuildProvider();

        var formatter = provider.GetRequiredKeyedService<IOpenGenericFormatter<int>>("default");

        Assert.Equal("42", formatter.Format(42));
        Assert.Null(provider.GetKeyedService<IOpenGenericFormatter<int>>("other"));
    }

    [Fact]
    public void TryAddEnumerable_TwoImplementations_BothResolveThroughEnumerable()
    {
        using var provider = BuildProvider();

        var handlers = provider.GetServices<IOpenGenericHandler<int>>().ToList();

        Assert.Equal(2, handlers.Count);
        Assert.Contains(handlers, h => h is OpenGenericHandlerA<int>);
        Assert.Contains(handlers, h => h is OpenGenericHandlerB<int>);
    }

    [Fact]
    public void TryAddEnumerable_ReRunningGeneratedRegistration_SuppressesDuplicates()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        // Re-run the exact same generated registrations a second time -- TryAddEnumerable must
        // suppress the resulting duplicate (ServiceType, ImplementationType) descriptors, exactly
        // as it would for a closed class.
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var handlers = provider.GetServices<IOpenGenericHandler<int>>().ToList();

        Assert.Equal(2, handlers.Count);
    }

    [Fact]
    public void Replace_RemovesManualRegistration_AndUsesGeneratedImplementation()
    {
        var services = new ServiceCollection();
        // Registered manually, as an open generic, before the generated extension method runs --
        // Replace must remove this registration in favor of the generated one.
        services.AddSingleton(typeof(IOpenGenericReplaceable<>), typeof(ManualOpenGenericReplaceable<>));
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IOpenGenericReplaceable<int>>();
        var all = provider.GetServices<IOpenGenericReplaceable<int>>().ToList();

        Assert.IsType<GeneratedOpenGenericReplaceable<int>>(resolved);
        Assert.Single(all);
    }

    [Fact]
    public void MultiInterfaceOpenGenericSingleton_ResolvesDifferentInstances()
    {
        // Documented, intentional divergence from the closed-class self+forwarding pattern:
        // forwarding is impossible for open generics (see OpenGenericStore's SSAL010 suppression),
        // so each interface gets its own independent registration and instance.
        using var provider = BuildProvider();

        var reader = provider.GetRequiredService<IOpenGenericReader<int>>();
        var writer = provider.GetRequiredService<IOpenGenericWriter<int>>();

        Assert.IsType<OpenGenericStore<int>>(reader);
        Assert.IsType<OpenGenericStore<int>>(writer);
        Assert.NotSame(reader, writer);
    }
}
