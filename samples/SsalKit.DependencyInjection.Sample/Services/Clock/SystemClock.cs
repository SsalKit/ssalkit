using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Clock;

[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd)]
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now() => DateTimeOffset.UtcNow;
}
