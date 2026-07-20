using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies <c>RegistrationMode</c> semantics: <c>TryAdd</c> only registers when the service type
/// has no existing registration, <c>TryAddEnumerable</c> allows multiple distinct implementations
/// to coexist for the same service type, and <c>Replace</c> removes any pre-existing registration
/// in favor of the generated one.
/// </summary>
public class ModeTests
{
    [Fact]
    public void TryAdd_WhenNoPriorRegistrationExists_RegistersGeneratedImplementation()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IModeTryAddContract>();

        Assert.IsType<GeneratedTryAddImpl>(resolved);
    }

    [Fact]
    public void TryAdd_WhenPriorRegistrationExists_KeepsExistingImplementation()
    {
        var services = new ServiceCollection();
        // Registered *before* the generated extension method runs -- TryAdd must see this and
        // skip its own registration.
        services.AddSingleton<IModeTryAddContract, ManualTryAddImpl>();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IModeTryAddContract>();
        var all = provider.GetServices<IModeTryAddContract>().ToList();

        Assert.IsType<ManualTryAddImpl>(resolved);
        Assert.Single(all);
    }

    [Fact]
    public void TryAddEnumerable_MultipleImplementations_AllResolveThroughEnumerable()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var all = provider.GetServices<IModeEnumerableContract>().ToList();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, s => s is EnumerableImplA);
        Assert.Contains(all, s => s is EnumerableImplB);
    }

    [Fact]
    public void Replace_RemovesPriorRegistration_AndUsesGeneratedImplementation()
    {
        var services = new ServiceCollection();
        // Registered *before* the generated extension method runs -- Replace must remove this
        // registration and install the generated one in its place.
        services.AddSingleton<IModeReplaceContract, ManualReplaceImpl>();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IModeReplaceContract>();
        var all = provider.GetServices<IModeReplaceContract>().ToList();

        Assert.IsType<GeneratedReplaceImpl>(resolved);
        Assert.Single(all);
    }
}
