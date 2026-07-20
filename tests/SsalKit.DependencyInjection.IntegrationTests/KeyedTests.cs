using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies keyed service registration/resolution: each key resolves to its own implementation,
/// and a key that was never registered resolves to <see langword="null"/> rather than falling
/// back to some other registration.
/// </summary>
public class KeyedTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Keyed_LoudKey_ResolvesLoudImplementation()
    {
        using var provider = BuildProvider();

        var formatter = provider.GetRequiredKeyedService<IVolumeFormatter>("loud");

        Assert.IsType<LoudVolumeFormatter>(formatter);
        Assert.Equal("HELLO!!!", formatter.Format("hello"));
    }

    [Fact]
    public void Keyed_QuietKey_ResolvesQuietImplementation()
    {
        using var provider = BuildProvider();

        var formatter = provider.GetRequiredKeyedService<IVolumeFormatter>("quiet");

        Assert.IsType<QuietVolumeFormatter>(formatter);
        Assert.Equal("hello", formatter.Format("HELLO"));
    }

    [Fact]
    public void Keyed_UnregisteredKey_ResolvesToNull()
    {
        using var provider = BuildProvider();

        var formatter = provider.GetKeyedService<IVolumeFormatter>("does-not-exist");

        Assert.Null(formatter);
    }

    [Fact]
    public void Keyed_NonKeyedResolution_DoesNotFindKeyedRegistration()
    {
        using var provider = BuildProvider();

        // Keyed registrations must not leak into a non-keyed GetService<T>() resolution.
        var formatter = provider.GetService<IVolumeFormatter>();

        Assert.Null(formatter);
    }
}
