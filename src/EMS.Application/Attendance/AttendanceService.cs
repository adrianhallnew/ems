using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Options;
using EMS.Application.Common.Security;
using EMS.Application.Common.Time;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Attendance;

/// <summary>Clocking in and out, reading records, and Admin corrections.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">The acting user. Own-data operations take no identifier.</param>
/// <param name="resolver">Derives attendance state; the single implementation of spec §3.3.7.</param>
/// <param name="classifier">Recognises the unique-index violation that guards double clock-in.</param>
/// <param name="clock">The only source of "today" and "now".</param>
/// <param name="settings">Supplies the page size ceiling.</param>
public sealed class AttendanceService(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    IAttendanceStateResolver resolver,
    IDatabaseErrorClassifier classifier,
    SctClock clock,
    IOptions<AppSettings> settings)
    : IAttendanceService
{
    /// <inheritdoc/>
    /// <remarks>
    /// The unique index on (Employee, Date) is the guard. The state check ahead of it is for the
    /// message, not the protection: two requests can both pass it (spec §3.3.4).
    /// </remarks>
    public async Task<Result> ClockInAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } employeeId)
        {
            return Result.Fail(ErrorCode.Forbidden, "No employee is signed in.");
        }

        var today = clock.Today;
        var state = await ResolveDayAsync(employeeId, today, ct).ConfigureAwait(false);

        if (state is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        if (Blocks(state.State, out var reason))
        {
            return Result.Fail(ErrorCode.BusinessRule, reason);
        }

        if (state.ClockIn is not null)
        {
            return Result.Fail(ErrorCode.Conflict, "Already clocked in today.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            Date = today,
            ClockIn = clock.UtcNow.UtcDateTime,
        });

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (classifier.IsUniqueViolation(exception))
        {
            return Result.Fail(ErrorCode.Conflict, "Already clocked in today.");
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ClockOutAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } employeeId)
        {
            return Result.Fail(ErrorCode.Forbidden, "No employee is signed in.");
        }

        var today = clock.Today;

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var record = await db.AttendanceRecords
            .SingleOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today, ct)
            .ConfigureAwait(false);

        if (record?.ClockIn is null)
        {
            return Result.Fail(ErrorCode.BusinessRule, "You have not clocked in today.");
        }

        if (record.ClockOut is not null)
        {
            return Result.Fail(ErrorCode.Conflict, "Already clocked out today.");
        }

        var now = clock.UtcNow.UtcDateTime;

        if (now < record.ClockIn.Value)
        {
            return Result.Fail(ErrorCode.BusinessRule, "Clock out cannot precede clock in.");
        }

        record.ClockOut = now;
        record.WorkedMinutes = (int)(now - record.ClockIn.Value).TotalMinutes;

        // A completed day is no longer a missed clock-out, whatever the nightly job decided.
        record.IsFlagged = false;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<AttendanceTodayDto?> GetTodayAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } employeeId)
        {
            return null;
        }

        var today = clock.Today;
        var day = await ResolveDayAsync(employeeId, today, ct).ConfigureAwait(false);

        if (day is null)
        {
            return null;
        }

        var blocked = Blocks(day.State, out _);

        return new AttendanceTodayDto(
            today,
            day.State,
            day.ClockIn,
            day.ClockOut,
            day.WorkedMinutes,
            CanClockIn: !blocked && day.ClockIn is null,
            CanClockOut: !blocked && day.ClockIn is not null && day.ClockOut is null);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Paging is applied to the resolved days rather than to the stored records: an absence has no
    /// row, so paging the table would page the wrong population (ADR-0004).
    /// </remarks>
    public async Task<PagedResult<AttendanceDayDto>> GetRecordsAsync(
        AttendanceFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        if (filter.To < filter.From)
        {
            return PagedResult<AttendanceDayDto>.Empty(page, pageSize);
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        // Scope first, filters second: a Manager's department is not a filter the caller can widen
        // (architecture.md §3.4).
        var employees = db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ForUser(currentUser);

        if (filter.DepartmentId is { } departmentId)
        {
            employees = employees.Where(e => e.DepartmentId == departmentId);
        }

        if (filter.EmployeeId is { } employeeId)
        {
            employees = employees.Where(e => e.Id == employeeId);
        }

        var ids = await employees
            .Select(e => e.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (ids.Count == 0)
        {
            return PagedResult<AttendanceDayDto>.Empty(page, pageSize);
        }

        var days = await resolver
            .ResolveAsync(ids, filter.From, filter.To, ct)
            .ConfigureAwait(false);

        IEnumerable<AttendanceDayDto> filtered = days;

        if (filter.State is { } state)
        {
            filtered = filtered.Where(d => d.State == state);
        }

        if (filter.FlaggedOnly)
        {
            filtered = filtered.Where(d => d.IsFlagged);
        }

        var ordered = AttendanceDaySort
            .Apply(filtered, filter.SortBy, filter.SortDescending)
            .ToList();

        var items = ordered
            .Skip(PageRequestExtensions.SkipFor(page, pageSize))
            .Take(pageSize)
            .ToList();

        return new PagedResult<AttendanceDayDto>(items, ordered.Count, page, pageSize);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A correction may create a record for a date that has none, but may never move one to another
    /// employee or date: those cases are a delete and a create, both audited (spec §3.3.6).
    /// </remarks>
    public async Task<Result> CorrectRecordAsync(
        CorrectAttendanceCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may correct attendance.");
        }

        if (command.ClockIn is null && command.ClockOut is not null)
        {
            return Result.Fail(ErrorCode.BusinessRule, "A clock out needs a clock in.");
        }

        if (command.ClockIn is { } start && command.ClockOut is { } end && end < start)
        {
            return Result.Fail(ErrorCode.BusinessRule, "Clock out cannot precede clock in.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employeeExists = await db.Employees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Id == command.EmployeeId, ct)
            .ConfigureAwait(false);

        if (!employeeExists)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        var record = command.RecordId is { } recordId
            ? await db.AttendanceRecords
                .SingleOrDefaultAsync(a => a.Id == recordId, ct)
                .ConfigureAwait(false)
            : null;

        if (command.RecordId is not null && record is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Attendance record not found.");
        }

        if (record is not null
            && (record.EmployeeId != command.EmployeeId || record.Date != command.Date))
        {
            return Result.Fail(
                ErrorCode.BusinessRule,
                "A correction cannot move a record to another employee or date. Delete it and create a new one.");
        }

        if (record is null)
        {
            record = new AttendanceRecord
            {
                EmployeeId = command.EmployeeId,
                Date = command.Date,
            };

            db.AttendanceRecords.Add(record);
        }

        record.ClockIn = command.ClockIn;
        record.ClockOut = command.ClockOut;
        record.WorkedMinutes = command.ClockIn is { } from && command.ClockOut is { } to
            ? (int)(to - from).TotalMinutes
            : null;

        // The flag exists to say "this day is unfinished". A correction is what finishes it.
        record.IsFlagged = false;
        record.CorrectionNote = command.CorrectionNote;
        record.CorrectedById = currentUser.EmployeeId;
        record.CorrectedAt = clock.UtcNow.UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (classifier.IsUniqueViolation(exception))
        {
            return Result.Fail(
                ErrorCode.Conflict,
                "That employee already has a record for that date.");
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteRecordAsync(Guid recordId, string reason, CancellationToken ct)
    {
        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may delete attendance.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Fail(ErrorCode.Validation, "A reason is required.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var record = await db.AttendanceRecords
            .SingleOrDefaultAsync(a => a.Id == recordId, ct)
            .ConfigureAwait(false);

        if (record is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Attendance record not found.");
        }

        // Two saves, one transaction. The reason has to be written before the delete so the audit
        // interceptor serialises it — it records the entity as it finds it — and both must land
        // together, or a failure between them leaves a record marked corrected but still present.
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            record.CorrectionNote = reason;
            record.CorrectedById = currentUser.EmployeeId;
            record.CorrectedAt = clock.UtcNow.UtcDateTime;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            db.AttendanceRecords.Remove(record);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            await tx.CommitAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>Resolves one employee's single day, or null when the employee does not exist.</summary>
    private async Task<AttendanceDayDto?> ResolveDayAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken ct)
    {
        var days = await resolver
            .ResolveAsync([employeeId], date, date, ct)
            .ConfigureAwait(false);

        return days.Count == 0 ? null : days[0];
    }

    /// <summary>
    /// Says whether the clock control is unavailable on a day, and why (spec §3.3.7).
    /// </summary>
    private static bool Blocks(AttendanceState state, out string reason)
    {
        reason = state switch
        {
            AttendanceState.Weekend => "The working week is Monday to Friday.",
            AttendanceState.Holiday => "Today is a public holiday.",
            AttendanceState.OnLeave => "You are on approved leave today.",
            AttendanceState.NotEmployed => "You are not employed on this date.",
            _ => string.Empty,
        };

        return reason.Length > 0;
    }
}
