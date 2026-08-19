using EMS.Application.Common.Models;

namespace EMS.Application.Notifications;

/// <summary>In-app notification reads and writes.</summary>
/// <remarks>
/// Notification rows are written inside the transaction that caused them, since they are ordinary
/// database writes. The publisher signal that updates the bell badge fires after commit — it is not
/// transactional, and a retry would send it twice.
/// </remarks>
public interface INotificationService
{
    /// <summary>Lists the acting employee's notifications.</summary>
    /// <param name="filter">Paging, sorting and the unread-only switch.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of notifications.</returns>
    Task<PagedResult<NotificationDto>> GetOwnAsync(NotificationFilter filter, CancellationToken ct);

    /// <summary>Counts the acting employee's unread notifications.</summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The unread count behind the bell badge.</returns>
    Task<int> GetUnreadCountAsync(CancellationToken ct);

    /// <summary>Marks one of the acting employee's notifications read.</summary>
    /// <param name="notificationId">The notification.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> MarkReadAsync(Guid notificationId, CancellationToken ct);

    /// <summary>Marks every one of the acting employee's notifications read.</summary>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> MarkAllReadAsync(CancellationToken ct);

    /// <summary>Raises a notification for one recipient.</summary>
    /// <param name="recipientId">The receiving employee.</param>
    /// <param name="title">The headline.</param>
    /// <param name="message">The body.</param>
    /// <param name="navigationUrl">Where clicking it goes, if anywhere.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> NotifyAsync(
        Guid recipientId,
        string title,
        string message,
        string? navigationUrl,
        CancellationToken ct);

    /// <summary>Raises a notification for every active Admin.</summary>
    /// <param name="title">The headline.</param>
    /// <param name="message">The body.</param>
    /// <param name="navigationUrl">Where clicking it goes, if anywhere.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// Fans out to one row per Admin. A notification targets a single recipient; there is no
    /// role-addressed notification (spec section 3.9.1).
    /// </remarks>
    Task<Result> NotifyAdminsAsync(
        string title,
        string message,
        string? navigationUrl,
        CancellationToken ct);
}
