using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: service-type determination.
//  - No directly-implemented interface -> registered as itself (self resolution).
//  - `As` explicitly specified -> registered only as that type, even though other interfaces
//    are implemented.

[Service(ServiceLifetime.Transient)]
public sealed class SelfRegisteredService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface IPrimaryContract
{
}

public interface ISecondaryContract
{
}

[Service(ServiceLifetime.Transient, As = typeof(IPrimaryContract))]
public sealed class AsSpecifiedService : IPrimaryContract, ISecondaryContract
{
}
