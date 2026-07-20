using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: AllowMultiple ([Service] applied more than once to the same class). One attribute
// registers the class as IAlphaContract, the other as IBetaContract, so both registrations must
// coexist and be independently resolvable.

public interface IAlphaContract
{
}

public interface IBetaContract
{
}

[Service(ServiceLifetime.Transient, As = typeof(IAlphaContract))]
[Service(ServiceLifetime.Transient, As = typeof(IBetaContract))]
public sealed class DualAsService : IAlphaContract, IBetaContract
{
}

// Covers: AllowMultiple with distinct Keys instead of distinct As types -- the same interface
// registered twice under two different keys on the same class.

public interface IMultiKeyedContract
{
}

[Service(ServiceLifetime.Singleton, Key = "key-a")]
[Service(ServiceLifetime.Singleton, Key = "key-b")]
public sealed class DualKeyedService : IMultiKeyedContract
{
}
