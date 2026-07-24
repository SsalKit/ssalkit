using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.Sample.Services.Clock;

namespace SsalKit.DependencyInjection.Sample.Services.Startup;

// Factory: the constructor is private, so the only way Microsoft.Extensions.DependencyInjection
// can produce an instance is through the static factory method named by Factory below -- there is
// no constructor-activation fallback to accidentally rely on. The factory takes IServiceProvider
// (rather than being parameterless), so it can pull IClock out of the container itself and stamp
// the banner text with it, exactly as the generated registration would do for a normal
// constructor parameter.
[Service(ServiceLifetime.Singleton, Factory = nameof(Create))]
public sealed class StartupBanner : IStartupBanner
{
    public string Text { get; }

    private StartupBanner(string text)
    {
        Text = text;
    }

    public static StartupBanner Create(IServiceProvider sp)
    {
        var clock = sp.GetRequiredService<IClock>();
        return new StartupBanner($"SsalKit.DependencyInjection sample started at {clock.Now():O}");
    }
}
