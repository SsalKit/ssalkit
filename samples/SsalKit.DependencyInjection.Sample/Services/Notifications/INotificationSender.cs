namespace SsalKit.DependencyInjection.Sample.Services.Notifications;

public interface INotificationSender
{
    string Send(string message);
}
