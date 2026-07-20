using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Messaging;

// No interface implemented -> registered as itself.
[Service(ServiceLifetime.Transient)]
public sealed class MessageBuilder
{
    public string Build(string greeting, string subject) => $"{greeting}, {subject}!";
}
