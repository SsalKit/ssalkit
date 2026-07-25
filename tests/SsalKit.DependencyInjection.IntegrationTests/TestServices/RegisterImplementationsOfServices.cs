using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: [assembly: RegisterImplementationsOf] convention scanning end-to-end through the real
// generator (see the .csproj's Analyzer project reference). The attributes are assembly-scoped, so
// every contract declared here is deliberately an interface used by nothing else in this test
// assembly -- a convention scan that reached into another TestServices file's types would change
// what its own tests observe.
[assembly: RegisterImplementationsOf(typeof(IConventionTask))]
[assembly: RegisterImplementationsOf(typeof(IConventionHandler<,>), ServiceLifetime.Scoped)]
[assembly: RegisterImplementationsOf(typeof(IConventionValidator<>), ServiceLifetime.Transient)]
[assembly: RegisterImplementationsOf(typeof(IConventionPolicy), Mode = RegistrationMode.TryAdd)]

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// ---------------------------------------------------------------------------------------------
// Non-generic contract, default (Singleton + TryAddEnumerable): the IEnumerable<T> injection shape
// a convention scan exists for.
// ---------------------------------------------------------------------------------------------

public interface IConventionTask
{
    string Name { get; }
}

public sealed class ConventionTaskA : IConventionTask
{
    public string Name => nameof(ConventionTaskA);
}

public sealed class ConventionTaskB : IConventionTask
{
    public string Name => nameof(ConventionTaskB);
}

// Registered by [Service], and therefore excluded from the scan: it must appear in the container
// exactly once, as a Transient, rather than additionally as a TryAddEnumerable Singleton.
[Service(ServiceLifetime.Transient)]
public sealed class ConventionTaskExplicit : IConventionTask
{
    public string Name => nameof(ConventionTaskExplicit);
}

// Abstract and (below) nested-private: silently skipped by the scan, with no diagnostic.
public abstract class ConventionTaskBase : IConventionTask
{
    public abstract string Name { get; }
}

// ...but a concrete class inheriting the contract from a base class does match.
public sealed class ConventionTaskDerived : ConventionTaskBase
{
    public override string Name => nameof(ConventionTaskDerived);
}

public static class ConventionTaskHost
{
    private sealed class Hidden : IConventionTask
    {
        public string Name => nameof(Hidden);
    }
}

// ---------------------------------------------------------------------------------------------
// Unbound generic contract: each implemented closed instantiation is registered independently.
// ---------------------------------------------------------------------------------------------

public interface IConventionHandler<TRequest, TResponse>
{
    Guid InstanceId { get; }
}

public sealed record ConventionPing;

public sealed record ConventionPong;

public sealed record ConventionTick;

public sealed record ConventionTock;

public sealed class ConventionPingHandler : IConventionHandler<ConventionPing, ConventionPong>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// One class, two closed instantiations of the contract -- registered under both.
public sealed class ConventionDualHandler
    : IConventionHandler<ConventionTick, ConventionTock>, IConventionHandler<ConventionPong, ConventionPing>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// ---------------------------------------------------------------------------------------------
// Unbound generic contract with an open generic implementation (exact-match shape), which the
// generator emits through the typeof(...)-based registration overloads.
// ---------------------------------------------------------------------------------------------

public interface IConventionValidator<T>
{
    Guid InstanceId { get; }
}

public sealed class ConventionValidator<T> : IConventionValidator<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// ---------------------------------------------------------------------------------------------
// Non-default Mode: TryAdd binds a single implementation, first-registration-wins.
// ---------------------------------------------------------------------------------------------

public interface IConventionPolicy
{
    string Name { get; }
}

public sealed class ConventionPolicyA : IConventionPolicy
{
    public string Name => nameof(ConventionPolicyA);
}

public sealed class ConventionPolicyB : IConventionPolicy
{
    public string Name => nameof(ConventionPolicyB);
}
