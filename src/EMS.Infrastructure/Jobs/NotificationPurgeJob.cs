using EMS.Application.Common.Options;
using EMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EMS.Infrastructure.Jobs;

/// <summary>
/// Deletes notifications past their retention period.
/// </summary>
/// <param name="scopeFactory">Creates a scope per pass.</param>
/// <param name="time">The clock.</param>
/// <param name="settings">Supplies the SCT offset and the retention period.</param>
/// <param name="logger">Records each pass.</param>
/// <remarks>
/// Retention is a cutoff rather than a per-date sweep, so processing one date deletes everything
/// older than the window regardless of which date it is. The job still rides the catch-up loop for
/// its watermark and its schedule; the effect of a caught-up pass is simply the same delete run
/// once per outstanding date, which is idempotent (spec §3.9.2, architecture.md §4.10).
/// </remarks>
public sealed class NotificationPurgeJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<AppSettings> settings,
    ILogger<NotificationPurgeJob> logger)
    : CatchUpJob(scopeFactory, time, settings, logger)
{
    /// <inheritdoc/>
    protected override string JobName => "NotificationPurge";

    /// <summary>
    /// One day. A date-based purge has nothing to catch up on: the cutoff is the same whether the
    /// job missed one day or twenty.
    /// </summary>
    protected override int InitialCatchUpDays => 1;

    /// <inheritdoc/>
    protected override async Task ProcessDateAsync(
        DateOnly sctDate,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);

        var cutoff = Time.GetUtcNow().UtcDateTime.AddDays(-Settings.NotificationRetentionDays);

        // ExecuteDeleteAsync rather than load-then-remove: nothing here needs the rows, and the
        // audit interceptor does not track Notification.
        var deleted = await db.Notifications
            .Where(n => n.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            Logger.LogInformation(
                "Purged {Count} notification(s) older than {Cutoff:u}.",
                deleted,
                cutoff);
        }
    }
}
