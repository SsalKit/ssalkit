using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: keyed service registration. Two implementations of the same interface are registered
// under distinct string keys; each key must resolve to its own implementation, and a key that
// was never registered must resolve to null via GetKeyedService.

public interface IVolumeFormatter
{
    string Format(string message);
}

[Service(ServiceLifetime.Singleton, As = typeof(IVolumeFormatter), Key = "loud")]
public sealed class LoudVolumeFormatter : IVolumeFormatter
{
    public string Format(string message) => message.ToUpperInvariant() + "!!!";
}

[Service(ServiceLifetime.Singleton, As = typeof(IVolumeFormatter), Key = "quiet")]
public sealed class QuietVolumeFormatter : IVolumeFormatter
{
    public string Format(string message) => message.ToLowerInvariant();
}
