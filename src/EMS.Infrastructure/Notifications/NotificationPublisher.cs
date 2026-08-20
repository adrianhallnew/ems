using System.Collections.Concurrent;
using EMS.Application.Notifications;

namespace EMS.Infrastructure.Notifications;

/// <summary>
/// In-process publish/subscribe for the notification bell.
/// </summary>
/// <remarks>
/// Without a push mechanism an unread badge only updates on navigation (spec §3.9.2). A
/// single-instance deployment needs nothing more than this; it is explicitly not distributed, and
/// it is the first thing that must change if the application is ever scaled out
/// (architecture.md §4.9).
/// <para>
/// Subscriptions are held as weak references, so a circuit that drops without disposing its
/// subscription cannot keep the component alive. Registered as a singleton: the whole point is that
/// one recipient's subscription outlives the scope that raised the notification.
/// </para>
/// </remarks>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly ConcurrentDictionary<Guid, List<WeakReference<Action>>> _subscriptions = new();

    /// <inheritdoc/>
    public void Publish(Guid recipientId)
    {
        if (!_subscriptions.TryGetValue(recipientId, out var handlers))
        {
            return;
        }

        List<Action> live = [];

        lock (handlers)
        {
            handlers.RemoveAll(reference => !reference.TryGetTarget(out _));

            foreach (var reference in handlers)
            {
                if (reference.TryGetTarget(out var handler))
                {
                    live.Add(handler);
                }
            }
        }

        foreach (var handler in live)
        {
            // One failing subscriber must not stop the others: a disposing circuit can throw here.
            try
            {
                handler();
            }
#pragma warning disable CA1031 // A subscriber's failure is not this publisher's to interpret.
            catch (Exception)
#pragma warning restore CA1031
            {
                // Ignored deliberately.
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Guid recipientId, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var handlers = _subscriptions.GetOrAdd(recipientId, _ => []);

        var reference = new WeakReference<Action>(handler);

        lock (handlers)
        {
            handlers.Add(reference);
        }

        return new Subscription(this, recipientId, reference);
    }

    private void Unsubscribe(Guid recipientId, WeakReference<Action> reference)
    {
        if (!_subscriptions.TryGetValue(recipientId, out var handlers))
        {
            return;
        }

        lock (handlers)
        {
            handlers.Remove(reference);
        }
    }

    /// <summary>The token a subscriber disposes to stop listening.</summary>
    private sealed class Subscription(
        NotificationPublisher publisher,
        Guid recipientId,
        WeakReference<Action> reference)
        : IDisposable
    {
        public void Dispose() => publisher.Unsubscribe(recipientId, reference);
    }
}
