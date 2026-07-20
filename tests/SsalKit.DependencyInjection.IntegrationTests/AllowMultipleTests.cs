using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies that a class decorated with more than one <c>[Service]</c> attribute (allowed via
/// <c>AttributeUsage.AllowMultiple</c>) produces every registration described by each attribute
/// independently.
/// </summary>
public class AllowMultipleTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DistinctAsTypes_BothRegistrationsAreResolvable()
    {
        using var provider = BuildProvider();

        var alpha = provider.GetService<IAlphaContract>();
        var beta = provider.GetService<IBetaContract>();

        Assert.NotNull(alpha);
        Assert.IsType<DualAsService>(alpha);
        Assert.NotNull(beta);
        Assert.IsType<DualAsService>(beta);
    }

    [Fact]
    public void DistinctKeys_BothRegistrationsAreIndependentlyResolvable()
    {
        using var provider = BuildProvider();

        var keyA = provider.GetRequiredKeyedService<IMultiKeyedContract>("key-a");
        var keyB = provider.GetRequiredKeyedService<IMultiKeyedContract>("key-b");

        Assert.IsType<DualKeyedService>(keyA);
        Assert.IsType<DualKeyedService>(keyB);
    }
}
