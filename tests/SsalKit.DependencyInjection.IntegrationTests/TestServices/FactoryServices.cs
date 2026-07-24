using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: [Service(Factory = "...")]. Every class below has a *private* constructor, so the only
// way Microsoft.Extensions.DependencyInjection can possibly construct an instance is through the
// generated factory-delegate registration -- if the generator ever stopped invoking the factory
// (e.g. fell back to constructor activation), resolution would throw
// InvalidOperationException/MissingMethodException instead of succeeding, making these tests a
// strong proof that the factory is actually invoked, not just that *some* instance appears.

public interface IParameterlessFactoryContract
{
    string Marker { get; }
}

[Service(ServiceLifetime.Singleton, As = typeof(IParameterlessFactoryContract), Factory = nameof(ParameterlessFactoryService.Create))]
public sealed class ParameterlessFactoryService : IParameterlessFactoryContract
{
    public string Marker { get; }

    private ParameterlessFactoryService(string marker)
    {
        Marker = marker;
    }

    public static ParameterlessFactoryService Create() => new("created-by-parameterless-factory");
}

public interface IServiceProviderFactoryContract
{
    IParameterlessFactoryContract Dependency { get; }
}

// The factory pulls a real dependency out of the IServiceProvider it's handed, proving the
// generated lambda actually forwards the container's IServiceProvider rather than some
// unconnected/empty one.
[Service(ServiceLifetime.Singleton, As = typeof(IServiceProviderFactoryContract), Factory = nameof(ServiceProviderFactoryService.Create))]
public sealed class ServiceProviderFactoryService : IServiceProviderFactoryContract
{
    public IParameterlessFactoryContract Dependency { get; }

    private ServiceProviderFactoryService(IParameterlessFactoryContract dependency)
    {
        Dependency = dependency;
    }

    public static ServiceProviderFactoryService Create(IServiceProvider sp) =>
        new(sp.GetRequiredService<IParameterlessFactoryContract>());
}

public interface IFactorySingletonContract
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Singleton, As = typeof(IFactorySingletonContract), Factory = nameof(FactorySingletonService.Create))]
public sealed class FactorySingletonService : IFactorySingletonContract
{
    public Guid Id { get; }

    private FactorySingletonService(Guid id)
    {
        Id = id;
    }

    public static FactorySingletonService Create() => new(Guid.NewGuid());
}

public interface IFactoryScopedContract
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Scoped, As = typeof(IFactoryScopedContract), Factory = nameof(FactoryScopedService.Create))]
public sealed class FactoryScopedService : IFactoryScopedContract
{
    public Guid Id { get; }

    private FactoryScopedService(Guid id)
    {
        Id = id;
    }

    public static FactoryScopedService Create() => new(Guid.NewGuid());
}

public interface IFactoryTransientContract
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Transient, As = typeof(IFactoryTransientContract), Factory = nameof(FactoryTransientService.Create))]
public sealed class FactoryTransientService : IFactoryTransientContract
{
    public Guid Id { get; }

    private FactoryTransientService(Guid id)
    {
        Id = id;
    }

    public static FactoryTransientService Create() => new(Guid.NewGuid());
}

public interface IKeyedFactoryContract
{
    string Origin { get; }
}

[Service(ServiceLifetime.Singleton, As = typeof(IKeyedFactoryContract), Key = "alpha", Factory = nameof(AlphaKeyedFactoryService.Create))]
public sealed class AlphaKeyedFactoryService : IKeyedFactoryContract
{
    public string Origin { get; }

    private AlphaKeyedFactoryService(string origin)
    {
        Origin = origin;
    }

    public static AlphaKeyedFactoryService Create() => new("alpha");
}

[Service(ServiceLifetime.Singleton, As = typeof(IKeyedFactoryContract), Key = "beta", Factory = nameof(BetaKeyedFactoryService.Create))]
public sealed class BetaKeyedFactoryService : IKeyedFactoryContract
{
    public string Origin { get; }

    private BetaKeyedFactoryService(string origin)
    {
        Origin = origin;
    }

    public static BetaKeyedFactoryService Create() => new("beta");
}

public interface IMultiInterfaceFactoryReaderContract
{
    Guid Id { get; }
}

public interface IMultiInterfaceFactoryWriterContract
{
    Guid Id { get; }
}

// No explicit As: 2+ directly-implemented interfaces + Singleton triggers the self-registration +
// forwarding pattern. Only the self-registration statement invokes the factory; both interfaces
// (and the concrete type) must resolve to that same, single factory-created instance.
[Service(ServiceLifetime.Singleton, Factory = nameof(MultiInterfaceFactoryService.Create))]
public sealed class MultiInterfaceFactoryService : IMultiInterfaceFactoryReaderContract, IMultiInterfaceFactoryWriterContract
{
    public Guid Id { get; }

    private MultiInterfaceFactoryService(Guid id)
    {
        Id = id;
    }

    public static MultiInterfaceFactoryService Create() => new(Guid.NewGuid());
}

public interface ITryAddEnumerableFactoryContract
{
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable, Factory = nameof(TryAddEnumerableFactoryService.Create))]
public sealed class TryAddEnumerableFactoryService : ITryAddEnumerableFactoryContract
{
    private TryAddEnumerableFactoryService()
    {
    }

    public static TryAddEnumerableFactoryService Create() => new();
}
