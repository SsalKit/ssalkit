using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: instance sharing across forwarded service types. When a class implements 2+
// interfaces and `As` is not specified, Singleton/Scoped lifetimes register the concrete type
// directly and forward every interface to that same instance via GetRequiredService, so
// resolving through either interface -- or the concrete type -- yields the same object.

public interface IReaderContract
{
    Guid Id { get; }
}

public interface IWriterContract
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Singleton)]
public sealed class MultiInterfaceSingletonService : IReaderContract, IWriterContract
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface IScopedReaderContract
{
    Guid Id { get; }
}

public interface IScopedWriterContract
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Scoped)]
public sealed class MultiInterfaceScopedService : IScopedReaderContract, IScopedWriterContract
{
    public Guid Id { get; } = Guid.NewGuid();
}
