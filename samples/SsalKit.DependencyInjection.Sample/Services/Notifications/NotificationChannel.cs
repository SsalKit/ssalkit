namespace SsalKit.DependencyInjection.Sample.Services.Notifications;

/// <summary>
/// The key type behind <see cref="INotificationSenderFactory"/>: every member (except
/// <see cref="Push"/>, deliberately left unregistered) has an <c>[Service(Key = ...)]</c>
/// implementation somewhere in this assembly.
/// </summary>
public enum NotificationChannel
{
    Email,
    Sms,

    /// <summary>Deliberately never registered, to show what an unknown key does at runtime.</summary>
    Push,
}
