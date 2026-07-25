namespace SsalKit.DependencyInjection.Sample.Services.Notifications;

/// <summary>
/// No class in this sample implements this interface: the source generator emits one (into the
/// reserved <c>SsalKit.DependencyInjection.Generated</c> namespace) and registers it as a
/// singleton, so it can be injected or resolved like any other service.
/// </summary>
[ServiceFactory]
public interface INotificationSenderFactory
{
    INotificationSender Create(NotificationChannel channel);
}
