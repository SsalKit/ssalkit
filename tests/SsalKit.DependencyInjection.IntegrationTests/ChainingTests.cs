using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies that the generated extension method returns the same <see cref="IServiceCollection"/>
/// instance it was called on, so it can participate in a fluent registration chain.
/// </summary>
public class ChainingTests
{
    [Fact]
    public void GeneratedExtensionMethod_ReturnsSameServiceCollectionInstance()
    {
        var services = new ServiceCollection();

        var result = services.AddSsalKitDependencyInjectionIntegrationTestsServices();

        Assert.Same(services, result);
    }
}
