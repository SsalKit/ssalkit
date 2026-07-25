using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// End-to-end tests for <c>[assembly: RegisterImplementationsOf]</c>, run through the real
/// generator (see the project's Analyzer reference), verifying that the convention-scanned
/// registrations actually resolve out of a real <c>ServiceProvider</c> as documented.
/// </summary>
public class RegisterImplementationsOfTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void NonGenericContract_InjectsEveryImplementation_AsAnEnumerable()
    {
        using var provider = BuildProvider();

        var names = provider.GetServices<IConventionTask>().Select(task => task.Name).ToList();

        Assert.Contains(nameof(ConventionTaskA), names);
        Assert.Contains(nameof(ConventionTaskB), names);
    }

    [Fact]
    public void ImplementationInheritedFromAnAbstractBase_IsRegistered()
    {
        using var provider = BuildProvider();

        var names = provider.GetServices<IConventionTask>().Select(task => task.Name).ToList();

        Assert.Contains(nameof(ConventionTaskDerived), names);
    }

    [Fact]
    public void AbstractAndInaccessibleImplementations_AreNotRegistered()
    {
        using var provider = BuildProvider();

        var typeNames = provider.GetServices<IConventionTask>().Select(task => task.GetType().Name).ToList();

        Assert.DoesNotContain("ConventionTaskBase", typeNames);
        Assert.DoesNotContain("Hidden", typeNames);
    }

    [Fact]
    public void ServiceDecoratedImplementation_IsRegisteredOnlyOnce_TheWayServiceSaid()
    {
        using var provider = BuildProvider();

        var explicitInstances = provider.GetServices<IConventionTask>()
            .Where(task => task is ConventionTaskExplicit)
            .ToList();

        // Registered by [Service] alone: had the scan also picked it up, there would be two.
        Assert.Single(explicitInstances);

        // ...and with [Service]'s Transient lifetime, not the scan's Singleton default.
        var first = provider.GetServices<IConventionTask>().First(task => task is ConventionTaskExplicit);
        var second = provider.GetServices<IConventionTask>().First(task => task is ConventionTaskExplicit);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void DefaultLifetime_IsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetServices<IConventionTask>().First(task => task is ConventionTaskA);
        var second = provider.GetServices<IConventionTask>().First(task => task is ConventionTaskA);

        Assert.Same(first, second);
    }

    [Fact]
    public void TryAddEnumerable_DoesNotRegisterTheSameImplementationTwice()
    {
        using var provider = BuildProvider();

        var implementationTypes = provider.GetServices<IConventionTask>().Select(task => task.GetType()).ToList();

        Assert.Equal(implementationTypes.Count, implementationTypes.Distinct().Count());
    }

    [Fact]
    public void UnboundGenericContract_ResolvesEachClosedInstantiationIndependently()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var pingHandlers = scope.ServiceProvider
            .GetServices<IConventionHandler<ConventionPing, ConventionPong>>()
            .ToList();

        var tickHandlers = scope.ServiceProvider
            .GetServices<IConventionHandler<ConventionTick, ConventionTock>>()
            .ToList();

        Assert.Single(pingHandlers);
        Assert.IsType<ConventionPingHandler>(pingHandlers[0]);

        Assert.Single(tickHandlers);
        Assert.IsType<ConventionDualHandler>(tickHandlers[0]);
    }

    [Fact]
    public void UnboundGenericContract_OneClassImplementingTwoInstantiations_IsRegisteredUnderBoth()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var asTick = scope.ServiceProvider.GetRequiredService<IConventionHandler<ConventionTick, ConventionTock>>();
        var asPong = scope.ServiceProvider.GetRequiredService<IConventionHandler<ConventionPong, ConventionPing>>();

        Assert.IsType<ConventionDualHandler>(asTick);
        Assert.IsType<ConventionDualHandler>(asPong);

        // Each matched service type gets its own, independent registration -- a convention scan
        // never forwards two service types onto one shared instance the way a multi-interface
        // [Service] does.
        Assert.NotSame(asTick, asPong);
    }

    [Fact]
    public void ExplicitLifetimeArgument_IsHonoured()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IConventionHandler<ConventionPing, ConventionPong>>();
        var second = scope.ServiceProvider.GetRequiredService<IConventionHandler<ConventionPing, ConventionPong>>();

        Assert.Same(first, second);

        using var otherScope = provider.CreateScope();
        var fromOtherScope = otherScope.ServiceProvider
            .GetRequiredService<IConventionHandler<ConventionPing, ConventionPong>>();

        Assert.NotSame(first, fromOtherScope);
    }

    [Fact]
    public void OpenGenericImplementation_ResolvesForAnyClosedServiceType()
    {
        using var provider = BuildProvider();

        var intValidator = provider.GetRequiredService<IConventionValidator<int>>();
        var stringValidator = provider.GetRequiredService<IConventionValidator<string>>();

        Assert.IsType<ConventionValidator<int>>(intValidator);
        Assert.IsType<ConventionValidator<string>>(stringValidator);
    }

    [Fact]
    public void OpenGenericImplementation_HonoursTheDeclaredLifetime()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IConventionValidator<int>>();
        var second = provider.GetRequiredService<IConventionValidator<int>>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void TryAddMode_BindsASingleImplementation()
    {
        using var provider = BuildProvider();

        var policies = provider.GetServices<IConventionPolicy>().ToList();

        Assert.Single(policies);

        // Registrations are emitted ordered by implementation type name, so the first TryAdd wins.
        Assert.IsType<ConventionPolicyA>(policies[0]);
    }
}
