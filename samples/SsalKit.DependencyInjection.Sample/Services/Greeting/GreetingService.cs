using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Greeting;

// No lifetime argument specified -> defaults to ServiceLifetime.Singleton.
[Service]
public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}
