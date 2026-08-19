using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Options;
using EMS.Application.Common.Time;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Attendance;

/// <summary>
/// Derives attendance states over a date range from three indexed queries.
/// </summary>
/// <param name="factory">Creates one short-lived context per call.</param>
/// <param name="clock">Converts stored UTC instants to SCT dates and times.</param>
/// <param name="settings">Supplies the fixed working day.</param>
/// <remarks>
/// The cost of deriving rather than storing is this projection: 50 employees across a 31-day month
/// is 1,550 rows assembled from three queries. The benefit is that no nightly job has to keep a
/// stored status consistent with leave approvals, holiday edits and hire dates forever (ADR-0004).
/// </remarks>
public sealed class AttendanceStateResolver(
    IApplicationDbContextFactory factory,
    SctClock clock,
    IOptions<AppSettings> settings) : IAttendanceStateResolver
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<AttendanceDayDto>> ResolveAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0 || endDate < startDate)
        {
            return [];
        }

        var ids = employeeIds.ToArray();

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        // Inactive employees are included deliberately: their historical days still belong in
        // reports and grids, and their DeactivatedAt is what stops later dates counting as absence.
        var employees = await db.Employees
            .IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new EmployeeWindow(
                e.Id,
                e.FirstName + " " + e.LastName,
                e.HireDate,
                e.DeactivatedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var records = await db.AttendanceRecords
            .Where(a => ids.Contains(a.EmployeeId) && a.Date >= startDate && a.Date <= endDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var holidays = (await db.PublicHolidays
            .Where(h => h.Date >= startDate && h.Date <= endDate)
            .Select(h => h.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .ToHashSet();

        var leave = await db.LeaveRequests
            .Where(r => ids.Contains(r.EmployeeId)
                        && r.Status == LeaveStatus.Approved
                        && r.StartDate <= endDate
                        && r.EndDate >= startDate)
            .Select(r => new LeaveWindow(r.EmployeeId, r.StartDate, r.EndDate))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var recordsByKey = records.ToDictionary(r => (r.EmployeeId, r.Date));
        var leaveByEmployee = leave.ToLookup(l => l.EmployeeId);

        var workDayStart = new TimeOnly(settings.Value.WorkDayStartHour, 0);
        var workDayEnd = new TimeOnly(settings.Value.WorkDayEndHour, 0);

        var results = new List<AttendanceDayDto>();

        foreach (var employee in employees)
        {
            var lastEmployedDate = employee.DeactivatedAt is { } deactivated
                ? clock.DateOf(deactivated)
                : (DateOnly?)null;

            var employeeLeave = leaveByEmployee[employee.Id].ToList();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                recordsByKey.TryGetValue((employee.Id, date), out var record);

                var clockInSct = record?.ClockIn is { } clockIn ? clock.TimeOf(clockIn) : (TimeOnly?)null;
                var clockOutSct = record?.ClockOut is { } clockOut ? clock.TimeOf(clockOut) : (TimeOnly?)null;

                var state = AttendanceStateRules.Resolve(
                    date,
                    employee.HireDate,
                    lastEmployedDate,
                    holidays.Contains(date),
                    employeeLeave.Exists(l => date >= l.StartDate && date <= l.EndDate),
                    clockInSct,
                    workDayStart);

                // Days outside the employment window are excluded from every count, so they are
                // not returned at all rather than returned and filtered by each caller.
                if (state == AttendanceState.NotEmployed)
                {
                    continue;
                }

                results.Add(new AttendanceDayDto(
                    employee.Id,
                    employee.FullName,
                    date,
                    state,
                    record?.ClockIn,
                    record?.ClockOut,
                    record?.WorkedMinutes,
                    record?.IsFlagged ?? false,
                    AttendanceStateRules.IsEarlyDeparture(clockOutSct, workDayEnd),
                    record?.CorrectionNote,
                    record?.Id));
            }
        }

        return results;
    }

    /// <summary>The employment window a state resolution needs.</summary>
    /// <param name="Id">The employee key.</param>
    /// <param name="FullName">The employee's display name.</param>
    /// <param name="HireDate">The first date of employment.</param>
    /// <param name="DeactivatedAt">When employment ended, in UTC, or null while active.</param>
    private sealed record EmployeeWindow(Guid Id, string FullName, DateOnly HireDate, DateTime? DeactivatedAt);

    /// <summary>One approved leave range.</summary>
    /// <param name="EmployeeId">The employee on leave.</param>
    /// <param name="StartDate">The first day of leave.</param>
    /// <param name="EndDate">The last day of leave.</param>
    private sealed record LeaveWindow(Guid EmployeeId, DateOnly StartDate, DateOnly EndDate);
}
