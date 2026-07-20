using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Session;

[Service(ServiceLifetime.Scoped)]
public sealed class SessionContext : ISessionContext
{
    public Guid SessionId { get; } = Guid.NewGuid();
}
