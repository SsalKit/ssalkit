using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: RegistrationMode behavior (TryAdd / TryAddEnumerable / Replace). Each contract below is
// only ever implemented by [Service]-decorated classes for one particular mode, plus a plain
// (non-[Service]) "manual" implementation that the tests register by hand to observe how the
// generated registration interacts with a pre-existing registration.

public interface IModeTryAddContract
{
    string Origin { get; }
}

// Manually registered by tests before calling Add...Services() to prove TryAdd is a no-op when a
// registration already exists.
public sealed class ManualTryAddImpl : IModeTryAddContract
{
    public string Origin => "manual";
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd)]
public sealed class GeneratedTryAddImpl : IModeTryAddContract
{
    public string Origin => "generated";
}

public interface IModeEnumerableContract
{
    string Name { get; }
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
public sealed class EnumerableImplA : IModeEnumerableContract
{
    public string Name => nameof(EnumerableImplA);
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
public sealed class EnumerableImplB : IModeEnumerableContract
{
    public string Name => nameof(EnumerableImplB);
}

public interface IModeReplaceContract
{
    string Origin { get; }
}

// Manually registered by tests before calling Add...Services() to prove Replace removes the
// pre-existing registration in favor of the generated one.
public sealed class ManualReplaceImpl : IModeReplaceContract
{
    public string Origin => "manual";
}

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace)]
public sealed class GeneratedReplaceImpl : IModeReplaceContract
{
    public string Origin => "generated";
}
