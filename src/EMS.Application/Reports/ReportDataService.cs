using EMS.Application.Attendance;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Application.Common.Time;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EMS.Application.Reports;

/// <summary>Assembles report rows. Rendering them is Infrastructure's job.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">The acting user, whose scope every report applies.</param>
/// <param name="resolver">Derives attendance state; the same component the grids use.</param>
/// <param name="clock">The only source of "today", which the presets are computed from.</param>
/// <remarks>
/// Scope is applied here, server-side, on every query. A Manager cannot widen it by changing the
/// request (spec §3.6.4). Salary appears in no report, for any role (spec §3.6.3).
/// </remarks>
public sealed class ReportDataService(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    IAttendanceStateResolver resolver,
    SctClock clock)
    : IReportDataService
{
    /// <inheritdoc/>
    /// <remarks>
    /// The arithmetic is in <see cref="ReportRangeRules"/>, which is pure and takes today as a
    /// parameter. Preset boundaries are SCT dates, consistent with spec §3.3.3.
    /// </remarks>
    public (DateOnly From, DateOnly To) ResolveRange(ReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ReportRangeRules.Resolve(request.Period, request.From, request.To, clock.Today);
    }

    /// <inheritdoc/>
    public async Task<AttendanceReportModel> GetAttendanceReportAsync(
        ReportRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (from, to) = ResolveRange(request);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employees = await ScopedEmployeesAsync(db, request.DepartmentId, ct).ConfigureAwait(false);

        if (employees.Count == 0)
        {
            return new AttendanceReportModel(
                from,
                to,
                await DepartmentNameAsync(db, request.DepartmentId, ct).ConfigureAwait(false),
                clock.UtcNow.UtcDateTime,
                []);
        }

        var days = await resolver
            .ResolveAsync([.. employees.Keys], from, to, ct)
            .ConfigureAwait(false);

        var rows = days
            .GroupBy(d => d.EmployeeId)
            .Select(group =>
            {
                var worked = group.Where(d => d.WorkedMinutes.HasValue).ToList();
                var totalMinutes = worked.Sum(d => d.WorkedMinutes!.Value);

                return new AttendanceReportRow(
                    group.First().EmployeeName,
                    employees[group.Key],
                    group.Count(d => d.State == AttendanceState.Present),
                    group.Count(d => d.State == AttendanceState.Late),
                    group.Count(d => d.State == AttendanceState.Absent),
                    group.Count(d => d.State == AttendanceState.OnLeave),
                    group.Count(d => d.State == AttendanceState.Holiday),
                    totalMinutes,
                    worked.Count == 0 ? 0 : totalMinutes / worked.Count,
                    group.Count(d => d.IsFlagged),
                    group.Count(d => d.CorrectionNote != null));
            })
            .OrderBy(row => row.EmployeeName)
            .ToList();

        return new AttendanceReportModel(
            from,
            to,
            await DepartmentNameAsync(db, request.DepartmentId, ct).ConfigureAwait(false),
            clock.UtcNow.UtcDateTime,
            rows);
    }

    /// <inheritdoc/>
    public async Task<LeaveReportModel> GetLeaveReportAsync(
        ReportRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (from, to) = ResolveRange(request);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employees = await ScopedEmployeesAsync(db, request.DepartmentId, ct).ConfigureAwait(false);
        var ids = employees.Keys.ToArray();

        if (ids.Length == 0)
        {
            return new LeaveReportModel(
                from,
                to,
                await DepartmentNameAsync(db, request.DepartmentId, ct).ConfigureAwait(false),
                clock.UtcNow.UtcDateTime,
                []);
        }

        var names = await db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct)
            .ConfigureAwait(false);

        // Balances overlapping the range, not only the current period: a report over last year must
        // show last year's entitlement.
        var balanceQuery = db.LeaveBalances
            .AsNoTracking()
            .Where(b => ids.Contains(b.EmployeeId) && b.PeriodStart <= to && b.PeriodEnd >= from);

        if (request.LeaveType is { } wantedType)
        {
            balanceQuery = balanceQuery.Where(b => b.LeaveType == wantedType);
        }

        var balances = await balanceQuery
            .Select(b => new
            {
                b.EmployeeId,
                b.LeaveType,
                b.Entitlement,
                b.Used,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var requestQuery = db.LeaveRequests
            .AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.StartDate <= to && r.EndDate >= from);

        if (request.LeaveType is { } filteredType)
        {
            requestQuery = requestQuery.Where(r => r.LeaveType == filteredType);
        }

        var requests = await requestQuery
            .Select(r => new { r.EmployeeId, r.LeaveType, r.Status })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var keys = balances
            .Select(b => (b.EmployeeId, b.LeaveType))
            .Concat(requests.Select(r => (r.EmployeeId, r.LeaveType)))
            .Distinct();

        var rows = keys
            .Select(key =>
            {
                var balance = balances.FirstOrDefault(b =>
                    b.EmployeeId == key.EmployeeId && b.LeaveType == key.LeaveType);

                var forKey = requests
                    .Where(r => r.EmployeeId == key.EmployeeId && r.LeaveType == key.LeaveType)
                    .ToList();

                return new LeaveReportRow(
                    names.TryGetValue(key.EmployeeId, out var name) ? name : string.Empty,
                    employees[key.EmployeeId],
                    key.LeaveType,
                    balance?.Entitlement ?? 0,
                    balance?.Used ?? 0,
                    balance is null ? 0 : balance.Entitlement - balance.Used,
                    forKey.Count(r => r.Status == LeaveStatus.Approved),
                    forKey.Count(r => r.Status == LeaveStatus.Rejected),
                    forKey.Count(r => r.Status == LeaveStatus.Cancelled),
                    forKey.Count(r => r.Status == LeaveStatus.Pending));
            })
            .OrderBy(row => row.EmployeeName)
            .ThenBy(row => row.LeaveType)
            .ToList();

        return new LeaveReportModel(
            from,
            to,
            await DepartmentNameAsync(db, request.DepartmentId, ct).ConfigureAwait(false),
            clock.UtcNow.UtcDateTime,
            rows);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Active employees only, and no salary column for any role (spec §3.6.3).
    /// </remarks>
    public async Task<DirectoryReportModel> GetDirectoryReportAsync(
        ReportRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        // Inactive employees are included: spec §3.6.3 lists Status as a column, which is a
        // constant unless a leaver can appear. Headcount below still counts the active ones.
        var scoped = db.Employees.AsNoTracking().IgnoreQueryFilters().ForUser(currentUser);

        if (request.DepartmentId is { } departmentId)
        {
            scoped = scoped.Where(e => e.DepartmentId == departmentId);
        }

        var people = await scoped
            .Select(e => new
            {
                e.Id,
                e.DepartmentId,
                FullName = e.FirstName + " " + e.LastName,
                e.JobTitle,
                e.ContractType,
                e.HireDate,
                e.Status,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var departmentIds = people.Select(p => p.DepartmentId).Distinct().ToArray();

        var departments = await db.Departments
            .AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name, d.ManagerId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var managerIds = departments
            .Where(d => d.ManagerId.HasValue)
            .Select(d => d.ManagerId!.Value)
            .ToArray();

        var managerNames = await db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => managerIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct)
            .ConfigureAwait(false);

        var groups = departments
            .Select(department =>
            {
                var members = people
                    .Where(p => p.DepartmentId == department.Id)
                    .OrderBy(p => p.FullName)
                    .Select(p => new DirectoryEmployeeRow(
                        p.FullName,
                        p.JobTitle,
                        p.ContractType,
                        p.HireDate,
                        p.Status))
                    .ToList();

                return new DirectoryDepartmentGroup(
                    department.Name,
                    department.ManagerId is { } managerId
                        ? managerNames.GetValueOrDefault(managerId)
                        : null,

                    // Headcount is who works here now, not who ever did.
                    members.Count(m => m.Status == EmployeeStatus.Active),
                    members);
            })
            .OrderBy(group => group.DepartmentName)
            .ToList();

        return new DirectoryReportModel(clock.UtcNow.UtcDateTime, groups);
    }

    /// <summary>
    /// The employees this caller may report on, mapped to their department name.
    /// </summary>
    /// <remarks>
    /// Inactive employees are included: a report over a past range must still account for someone
    /// who has since left (architecture.md §2.5).
    /// </remarks>
    private async Task<Dictionary<Guid, string>> ScopedEmployeesAsync(
        IApplicationDbContext db,
        Guid? departmentId,
        CancellationToken ct)
    {
        var query = db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ForUser(currentUser);

        if (departmentId is { } wanted)
        {
            query = query.Where(e => e.DepartmentId == wanted);
        }

        return await query
            .Select(e => new
            {
                e.Id,
                DepartmentName = db.Departments
                    .Where(d => d.Id == e.DepartmentId)
                    .Select(d => d.Name)
                    .FirstOrDefault() ?? string.Empty,
            })
            .ToDictionaryAsync(e => e.Id, e => e.DepartmentName, ct)
            .ConfigureAwait(false);
    }

    private static async Task<string?> DepartmentNameAsync(
        IApplicationDbContext db,
        Guid? departmentId,
        CancellationToken ct)
    {
        if (departmentId is not { } wanted)
        {
            return null;
        }

        return await db.Departments
            .AsNoTracking()
            .Where(d => d.Id == wanted)
            .Select(d => d.Name)
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
