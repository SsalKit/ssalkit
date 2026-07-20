using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: lifetime behavior (Singleton / Scoped / Transient) for a class with exactly one
// directly-implemented interface and no explicit `As`. Also doubles as the "As not specified,
// exactly one interface" service-type-resolution scenario: the generator should register the
// class only against the interface, never the concrete type, since a single-entry registration
// never requires the self + forwarding pattern.

public interface ISingletonMarkerService
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Singleton)]
public sealed class SingletonMarkerService : ISingletonMarkerService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface IScopedMarkerService
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Scoped)]
public sealed class ScopedMarkerService : IScopedMarkerService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface ITransientMarkerService
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Transient)]
public sealed class TransientMarkerService : ITransientMarkerService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface IDefaultLifetimeMarkerService
{
    Guid Id { get; }
}

// No lifetime argument supplied -> defaults to ServiceLifetime.Singleton.
[Service]
public sealed class DefaultLifetimeMarkerService : IDefaultLifetimeMarkerService
{
    public Guid Id { get; } = Guid.NewGuid();
}
