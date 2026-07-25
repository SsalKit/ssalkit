using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Notifications;

[Service(ServiceLifetime.Singleton, As = typeof(INotificationSender), Key = NotificationChannel.Sms)]
public sealed class SmsNotificationSender : INotificationSender
{
    public string Send(string message) => $"sms >> {message}";
}
