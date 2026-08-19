namespace EMS.Application.Notifications;

/// <summary>
/// Signals a live circuit that a notification arrived. Implemented in Infrastructure.
/// </summary>
/// <remarks>
/// A singleton in-process publisher with per-recipient subscriptions, consumed by the bell
/// component. Without it an unread badge only updates on navigation. This is explicitly not a
/// distributed mechanism, and it is the first thing that must change if the application is ever
/// scaled past one instance (ADR/architecture 4.9).
/// <para>Publishing happens after commit, never inside the transaction.</para>
/// </remarks>
public interface INotificationPublisher
{
    /// <summary>Signals one recipient that their unread set changed.</summary>
    /// <param name="recipientId">The receiving employee.</param>
    void Publish(Guid recipientId);

    /// <summary>Subscribes to one recipient's signals.</summary>
    /// <param name="recipientId">The receiving employee.</param>
    /// <param name="handler">Runs off the renderer's synchronisation context.</param>
    /// <returns>A token that unsubscribes when disposed.</returns>
    IDisposable Subscribe(Guid recipientId, Action handler);
}
