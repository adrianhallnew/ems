using EMS.Application.Common.Options;
using EMS.Application.Notifications;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using EMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EMS.Infrastructure.Jobs;

/// <summary>
/// Flags attendance records with a clock in and no clock out, once their SCT date has elapsed.
/// </summary>
/// <param name="scopeFactory">Creates a scope per pass.</param>
/// <param name="time">The clock.</param>
/// <param name="settings">Supplies the SCT offset.</param>
/// <param name="logger">Records each pass.</param>
/// <remarks>
/// Idempotent per date: a record already flagged is skipped, so a re-run notifies nobody twice
/// (spec §3.3.5).
/// </remarks>
public sealed class MissedClockOutJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<AppSettings> settings,
    ILogger<MissedClockOutJob> logger)
    : CatchUpJob(scopeFactory, time, settings, logger)
{
    /// <inheritdoc/>
    protected override string JobName => "MissedClockOut";

    /// <inheritdoc/>
    protected override async Task ProcessDateAsync(
        DateOnly sctDate,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);

        var unfinished = await db.AttendanceRecords
            .Where(a => a.Date == sctDate && a.ClockIn != null && a.ClockOut == null && !a.IsFlagged)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (unfinished.Count == 0)
        {
            return;
        }

        var admins = await db.Employees
            .Where(e => e.Role == EmployeeRole.Admin)
            .Select(e => e.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var names = await db.Employees
            .IgnoreQueryFilters()
            .Where(e => unfinished.Select(u => u.EmployeeId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct)
            .ConfigureAwait(false);

        foreach (var record in unfinished)
        {
            record.IsFlagged = true;

            var name = names.TryGetValue(record.EmployeeId, out var found) ? found : "An employee";

            foreach (var admin in admins)
            {
                db.Notifications.Add(new Notification
                {
                    RecipientId = admin,
                    Title = NotificationMessages.MissedClockOutTitle,
                    Message = NotificationMessages.MissedClockOut(name, sctDate),
                    NavigationUrl = "/attendance/all",
                });
            }
        }

        // The flags and their notices commit together with the watermark, so a failure here leaves
        // the date unprocessed rather than half-processed.
        Logger.LogInformation(
            "Flagged {Count} unfinished attendance record(s) for {SctDate}.",
            unfinished.Count,
            sctDate);
    }
}
