using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Options;
using EMS.Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Notifications;

/// <summary>The bell: a recipient's own notifications, and standalone fan-out.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">The acting user, who is the only recipient they may read.</param>
/// <param name="publisher">Signals a recipient's open circuit after a write.</param>
/// <param name="settings">Supplies the page size ceiling.</param>
/// <remarks>
/// The write methods here each commit on their own context, which is correct only for a
/// notification that is not part of a larger change. Anything raised by a business operation is
/// staged on that operation's context by <see cref="NotificationWriter"/> instead, so the rows
/// commit or roll back with the change that caused them (implementation.md §4.4).
/// </remarks>
public sealed class NotificationService(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    INotificationPublisher publisher,
    IOptions<AppSettings> settings)
    : INotificationService
{
    /// <inheritdoc/>
    public async Task<PagedResult<NotificationDto>> GetOwnAsync(
        NotificationFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        if (currentUser.EmployeeId is not { } recipientId)
        {
            return PagedResult<NotificationDto>.Empty(page, pageSize);
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var query = db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == recipientId);

        if (filter.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .ApplySort(filter.SortBy, filter.SortDescending)
            .Skip(PageRequestExtensions.SkipFor(page, pageSize))
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Title,
                n.Message,
                n.IsRead,
                n.NavigationUrl,
                n.CreatedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } recipientId)
        {
            return 0;
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        return await db.Notifications
            .CountAsync(n => n.RecipientId == recipientId && !n.IsRead, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Scoped to the acting user's own rows. Someone else's notification returns not found rather
    /// than forbidden, because distinguishing the two confirms it exists (architecture.md §3.4).
    /// </remarks>
    public async Task<Result> MarkReadAsync(Guid notificationId, CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } recipientId)
        {
            return Result.Fail(ErrorCode.NotFound, "Notification not found.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var notification = await db.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == recipientId, ct)
            .ConfigureAwait(false);

        if (notification is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Notification not found.");
        }

        notification.IsRead = true;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        publisher.Publish(recipientId);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> MarkAllReadAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } recipientId)
        {
            return Result.Success();
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var unread = await db.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        publisher.Publish(recipientId);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> NotifyAsync(
        Guid recipientId,
        string title,
        string message,
        string? navigationUrl,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var exists = await db.Employees
            .AnyAsync(e => e.Id == recipientId, ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            return Result.Fail(ErrorCode.NotFound, "Recipient not found.");
        }

        NotificationWriter.Stage(db, recipientId, title, message, navigationUrl);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        publisher.Publish(recipientId);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> NotifyAdminsAsync(
        string title,
        string message,
        string? navigationUrl,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var recipients = await NotificationWriter
            .StageForAdminsAsync(db, title, message, navigationUrl, ct)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var recipient in recipients)
        {
            publisher.Publish(recipient);
        }

        return Result.Success();
    }
}
