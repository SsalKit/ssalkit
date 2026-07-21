using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: typeof(...) as a keyed-service Key. KeyLiteralFormatter must format
// TypedConstantKind.Type constants as a `typeof(...)` expression; otherwise the Key argument is
// silently dropped and GetRequiredKeyedService<T>(typeof(...)) would fail to resolve anything.

public interface ITypeKeyedContract
{
}

public sealed class MarkerA
{
}

public sealed class MarkerB
{
}

[Service(ServiceLifetime.Singleton, As = typeof(ITypeKeyedContract), Key = typeof(MarkerA))]
public sealed class TypeKeyedServiceA : ITypeKeyedContract
{
}

[Service(ServiceLifetime.Singleton, As = typeof(ITypeKeyedContract), Key = typeof(MarkerB))]
public sealed class TypeKeyedServiceB : ITypeKeyedContract
{
}
