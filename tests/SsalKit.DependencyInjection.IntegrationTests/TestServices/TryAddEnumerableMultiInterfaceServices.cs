using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: RegistrationMode.TryAddEnumerable with 2+ directly-implemented interfaces. Unlike
// Add/TryAdd/Replace, this must NOT use the self-registration + forwarding-factory pattern --
// Microsoft.Extensions.DependencyInjection.ServiceCollectionDescriptorExtensions.TryAddEnumerable
// throws ArgumentException for a factory-based ServiceDescriptor (it has no implementation type to
// compare against for duplicate suppression). Instead, each interface must get its own independent
// ServiceDescriptor.Singleton<TService, TImpl>(), meaning (as documented, intentional behavior)
// resolving through different interfaces yields *different* instances -- no shared instance.

public interface IEnumerableReaderContract
{
}

public interface IEnumerableWriterContract
{
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
public sealed class MultiInterfaceEnumerableService : IEnumerableReaderContract, IEnumerableWriterContract
{
}
