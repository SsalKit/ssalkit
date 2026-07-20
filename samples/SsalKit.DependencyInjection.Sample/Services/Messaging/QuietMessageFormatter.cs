using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Messaging;

[Service(ServiceLifetime.Singleton, As = typeof(IMessageFormatter), Key = "quiet")]
public sealed class QuietMessageFormatter : IMessageFormatter
{
    public string Format(string message) => $"({message.ToLowerInvariant()})";
}
