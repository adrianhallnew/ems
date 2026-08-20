using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Options;
using EMS.Application.Common.Security;
using EMS.Application.Common.Time;
using EMS.Application.Notifications;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Leave;

/// <summary>Leave balance reads and Admin adjustments.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">The acting user, whose scope every read applies.</param>
/// <param name="publisher">Signals the bell after a commit.</param>
/// <param name="clock">The only source of "today".</param>
/// <param name="settings">Supplies the default entitlements.</param>
public sealed class LeaveBalanceService(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    INotificationPublisher publisher,
    SctClock clock,
    IOptions<AppSettings> settings)
    : ILeaveBalanceService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeaveBalanceDto>> GetOwnBalancesAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } ownId)
        {
            return [];
        }

        var result = await LoadAsync(ownId, ct).ConfigureAwait(false);

        return result.IsSuccess ? result.Value! : [];
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<LeaveBalanceDto>>> GetBalancesAsync(
        Guid employeeId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var inScope = await db.Employees
            .AsNoTracking()
            .ForUser(currentUser)
            .AnyAsync(e => e.Id == employeeId, ct)
            .ConfigureAwait(false);

        if (!inScope)
        {
            return Result<IReadOnlyList<LeaveBalanceDto>>.Fail(
                ErrorCode.NotFound,
                "Employee not found.");
        }

        return await LoadAsync(employeeId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The note is mandatory and is written in the same transaction as the change (spec §3.4.7).
    /// It lands on the balance row, so the audit interceptor records its before and after — which
    /// is where the history of adjustments lives.
    /// </remarks>
    public async Task<Result> AdjustAsync(AdjustLeaveBalanceCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may adjust a balance.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var outcome = Result.Fail(ErrorCode.BusinessRule, "Not executed.");
        Guid? notified = null;

        await ExecuteAsync(db, async () =>
        {
            var employee = await db.Employees
                .SingleOrDefaultAsync(e => e.Id == command.EmployeeId, ct)
                .ConfigureAwait(false);

            if (employee is null)
            {
                return Result.Fail(ErrorCode.NotFound, "Employee not found.");
            }

            var balances = await LeaveBalanceAccessor
                .EnsureCurrentPeriodAsync(db, employee, clock.Today, settings.Value.DefaultLeaveEntitlements, ct)
                .ConfigureAwait(false);

            var balance = balances.FirstOrDefault(b =>
                b.LeaveType == command.LeaveType && b.PeriodStart == command.PeriodStart);

            if (balance is null)
            {
                return Result.Fail(ErrorCode.NotFound, "No balance exists for that leave type and period.");
            }

            if (command.Entitlement < balance.Used)
            {
                return Result.Fail(
                    ErrorCode.BusinessRule,
                    $"The employee has already used {balance.Used} days in this period.");
            }

            balance.Entitlement = command.Entitlement;
            balance.LastAdjustmentNote = command.Note;

            NotificationWriter.Stage(
                db,
                employee.Id,
                NotificationMessages.BalanceAdjustedTitle,
                NotificationMessages.BalanceAdjusted(command.LeaveType),
                "/leave/my");

            notified = employee.Id;

            return Result.Success();
        }, result => outcome = result, ct).ConfigureAwait(false);

        Signal(outcome, notified);

        return outcome;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Maternity is never auto-created: an Admin grants it per qualifying event, with an explicit
    /// period and entitlement (spec §3.4.1, §3.4.2).
    /// </remarks>
    public async Task<Result> GrantMaternityAsync(
        GrantMaternityLeaveCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may grant maternity leave.");
        }

        if (command.PeriodEnd < command.PeriodStart)
        {
            return Result.Fail(ErrorCode.Validation, "The period ends before it starts.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var outcome = Result.Fail(ErrorCode.BusinessRule, "Not executed.");
        Guid? notified = null;

        await ExecuteAsync(db, async () =>
        {
            var employee = await db.Employees
                .SingleOrDefaultAsync(e => e.Id == command.EmployeeId, ct)
                .ConfigureAwait(false);

            if (employee is null)
            {
                return Result.Fail(ErrorCode.NotFound, "Employee not found.");
            }

            var granted = await db.LeaveBalances
                .AnyAsync(
                    b => b.EmployeeId == command.EmployeeId
                         && b.LeaveType == LeaveType.Maternity
                         && b.PeriodStart == command.PeriodStart,
                    ct)
                .ConfigureAwait(false);

            if (granted)
            {
                return Result.Fail(
                    ErrorCode.Conflict,
                    "Maternity leave has already been granted for that period.");
            }

            db.LeaveBalances.Add(new LeaveBalance
            {
                EmployeeId = command.EmployeeId,
                LeaveType = LeaveType.Maternity,
                PeriodStart = command.PeriodStart,
                PeriodEnd = command.PeriodEnd,
                Entitlement = command.Entitlement,
                Used = 0,
                LastAdjustmentNote = command.Note,
            });

            NotificationWriter.Stage(
                db,
                employee.Id,
                NotificationMessages.BalanceAdjustedTitle,
                NotificationMessages.BalanceAdjusted(LeaveType.Maternity),
                "/leave/my");

            notified = employee.Id;

            return Result.Success();
        }, result => outcome = result, ct).ConfigureAwait(false);

        Signal(outcome, notified);

        return outcome;
    }

    /// <summary>
    /// Runs one mutation as a retriable unit: execution strategy outside, transaction inside.
    /// </summary>
    /// <param name="db">The context the body works on.</param>
    /// <param name="body">Stages the change and reports whether it should commit.</param>
    /// <param name="report">Receives the result, which has to survive the delegate.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <remarks>
    /// A balance adjustment is the fourth flow implementation.md §4.2 names. The body can run more
    /// than once, so it stages database writes only; the bell is signalled after the commit.
    /// </remarks>
    private static async Task ExecuteAsync(
        IApplicationDbContext db,
        Func<Task<Result>> body,
        Action<Result> report,
        CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var result = await body().ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                report(result);
                return;
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                report(result);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                report(Result.Fail(
                    ErrorCode.ConcurrencyConflict,
                    "Someone else changed this balance. Please retry."));
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Signals the bell, only after a commit and only on success.</summary>
    private void Signal(Result outcome, Guid? recipient)
    {
        if (outcome.IsSuccess && recipient is { } id)
        {
            publisher.Publish(id);
        }
    }

    private async Task<Result<IReadOnlyList<LeaveBalanceDto>>> LoadAsync(
        Guid employeeId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == employeeId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result<IReadOnlyList<LeaveBalanceDto>>.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        // A read materialises the period too: spec §3.4.2 requires it before any balance read.
        var balances = await LeaveBalanceAccessor
            .EnsureCurrentPeriodAsync(db, employee, clock.Today, settings.Value.DefaultLeaveEntitlements, ct)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        IReadOnlyList<LeaveBalanceDto> items =
        [
            .. balances
                .OrderByDescending(b => b.PeriodStart)
                .ThenBy(b => b.LeaveType)
                .Select(b => new LeaveBalanceDto(
                    b.LeaveType,
                    b.PeriodStart,
                    b.PeriodEnd,
                    b.Entitlement,
                    b.Used,
                    b.Remaining)),
        ];

        return Result<IReadOnlyList<LeaveBalanceDto>>.Success(items);
    }
}
