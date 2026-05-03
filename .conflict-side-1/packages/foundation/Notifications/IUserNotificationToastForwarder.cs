namespace Sunfish.Foundation.Notifications;

/// <summary>
/// Seam for presenting a <see cref="UserNotification"/> on a toast surface.
/// Implementations live in UI adapters (Blazor, React) because they depend
/// on the adapter's toast host. The canonical <see cref="IUserNotificationService"/>
/// calls this when a notification's <see cref="UserNotification.Delivery"/> includes
/// <see cref="NotificationDelivery.ToastOnly"/>.
/// </summary>
public interface IUserNotificationToastForwarder
{
    void Forward(UserNotification notification);
}
