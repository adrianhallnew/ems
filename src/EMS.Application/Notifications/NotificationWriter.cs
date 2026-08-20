using EMS.Application.Common.Interfaces;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EMS.Application.Notifications;

/// <summary>
/// Stages notification rows on the caller's context.
/// </summary>
/// <remarks>
/// Notifications are ordinary database writes, so they belong in the transaction that commits the
/// change they describe: a leave request and the Admins' notice of it either both exist or neither
/// does (implementation.md §4.4). Staging them here rather than through
/// <see cref="INotificationService"/> is what keeps them in that transaction — the service creates
/// its own context and would commit separately.
/// <para>
/// Nothing here signals the bell. The in-process publisher is not transactional and a retried
/// execution strategy would fire it twice, so the caller publishes after the commit succeeds.
/// </para>
/// <para>
/// <c>CreatedAt</c> is stamped by the auditable-entity interceptor, so no clock is needed.
/// </para>
/// </remarks>
internal static class NotificationWriter
{
    /// <summary>Stages one notification for one recipient.</summary>
    /// <param name="db">The caller's context, inside the caller's transaction.</param>
    /// <param name="recipientId">The employee to notify.</param>
    /// <param name="title">The bell headline.</param>
    /// <param name="message">The body, already formatted per spec §3.9.1.</param>
    /// <param name="navigationUrl">Where clicking the notification goes, or null.</param>
    public static void Stage(
        IApplicationDbContext db,
        Guid recipientId,
        string title,
        string message,
        string? navigationUrl)
    {
        ArgumentNullException.ThrowIfNull(db);

        db.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            Title = title,
            Message = message,
            NavigationUrl = navigationUrl,
        });
    }

    /// <summary>Stages one notification per active Admin.</summary>
    /// <param name="db">The caller's context, inside the caller's transaction.</param>
    /// <param name="title">The bell headline.</param>
    /// <param name="message">The body, already formatted per spec §3.9.1.</param>
    /// <param name="navigationUrl">Where clicking the notification goes, or null.</param>
    /// <param name="ct">Cancels the recipient lookup.</param>
    /// <returns>The recipients, so the caller can signal each of them after the commit.</returns>
    /// <remarks>
    /// Fan-out is one row per Admin: <see cref="Notification"/> targets a single recipient and there
    /// is no role-addressed notification (spec §3.9.1). The employee query filter already excludes
    /// inactive employees.
    /// </remarks>
    public static async Task<IReadOnlyList<Guid>> StageForAdminsAsync(
        IApplicationDbContext db,
        string title,
        string message,
        string? navigationUrl,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);

        var admins = await db.Employees
            .Where(e => e.Role == EmployeeRole.Admin)
            .Select(e => e.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var admin in admins)
        {
            Stage(db, admin, title, message, navigationUrl);
        }

        return admins;
    }
}
