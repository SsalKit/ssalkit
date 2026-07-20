using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Messaging;

[Service(ServiceLifetime.Singleton, As = typeof(IMessageFormatter), Key = "loud")]
public sealed class LoudMessageFormatter : IMessageFormatter
{
    public string Format(string message) => message.ToUpperInvariant() + "!!!";
}
