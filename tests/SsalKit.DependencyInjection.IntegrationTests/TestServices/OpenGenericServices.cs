using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: open generic [Service] registration end-to-end through the real generator (see the
// .csproj's Analyzer project reference). Each contract below is scoped to one specific open
// generic behavior, mirroring the non-generic TestServices files' one-concern-per-file style.

public interface IOpenGenericRepository<T>
{
    Guid InstanceId { get; }
}

[Service(ServiceLifetime.Singleton)]
public sealed class OpenGenericRepository<T> : IOpenGenericRepository<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface ITransientOpenGenericRepository<T>
{
    Guid InstanceId { get; }
}

[Service(ServiceLifetime.Transient)]
public sealed class TransientOpenGenericRepository<T> : ITransientOpenGenericRepository<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// Arity-2 service: confirms the "<,>" typeof-form placeholder round-trips through a real
// MEDI registration/resolution, not just through the generated source text.
public interface IOpenGenericPair<TKey, TValue>
{
    Guid InstanceId { get; }
}

[Service(ServiceLifetime.Singleton)]
public sealed class OpenGenericPair<TKey, TValue> : IOpenGenericPair<TKey, TValue>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// Keyed open generic resolution.
public interface IOpenGenericFormatter<T>
{
    string Format(T value);
}

[Service(ServiceLifetime.Singleton, As = typeof(IOpenGenericFormatter<>), Key = "default")]
public sealed class DefaultOpenGenericFormatter<T> : IOpenGenericFormatter<T>
{
    public string Format(T value) => value?.ToString() ?? "<null>";
}

// TryAddEnumerable: two open generic implementations of the same interface must both resolve
// through IEnumerable<>, and re-running the generated registration must not duplicate them.
public interface IOpenGenericHandler<T>
{
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
public sealed class OpenGenericHandlerA<T> : IOpenGenericHandler<T>
{
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
public sealed class OpenGenericHandlerB<T> : IOpenGenericHandler<T>
{
}

// Replace behavior.
public interface IOpenGenericReplaceable<T>
{
    string Origin { get; }
}

// Registered manually by the test (as an open generic, exactly like the generated code would)
// before calling Add...Services() -- Replace must remove this registration in favor of the
// generated one.
public sealed class ManualOpenGenericReplaceable<T> : IOpenGenericReplaceable<T>
{
    public string Origin => "manual";
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace)]
public sealed class GeneratedOpenGenericReplaceable<T> : IOpenGenericReplaceable<T>
{
    public string Origin => "generated";
}

// Multi-interface open generic singleton: documented, intentional divergence from the closed-class
// self+forwarding pattern -- forwarding is impossible for open generics, so IReader/IWriter each
// get an independent registration and resolve to *different* instances.
public interface IOpenGenericReader<T>
{
    Guid InstanceId { get; }
}

public interface IOpenGenericWriter<T>
{
    Guid InstanceId { get; }
}

#pragma warning disable SSAL010 // intentional for this test: see IOpenGenericReader/IOpenGenericWriter above.
[Service(ServiceLifetime.Singleton)]
public sealed class OpenGenericStore<T> : IOpenGenericReader<T>, IOpenGenericWriter<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}
#pragma warning restore SSAL010
