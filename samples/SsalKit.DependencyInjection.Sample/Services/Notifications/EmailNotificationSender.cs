using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Notifications;

[Service(ServiceLifetime.Singleton, As = typeof(INotificationSender), Key = NotificationChannel.Email)]
public sealed class EmailNotificationSender : INotificationSender
{
    public string Send(string message) => $"email >> {message}";
}
