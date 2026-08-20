using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Options;
using EMS.Application.Common.Security;
using EMS.Application.Common.Time;
using EMS.Application.Notifications;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Leave;

/// <summary>Leave submission, decisions, and cancellation.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">The acting user. Submission takes no employee identifier.</param>
/// <param name="publisher">Signals the bell, always after a commit.</param>
/// <param name="submitValidator">Field-level rules for a submission.</param>
/// <param name="clock">The only source of "today".</param>
/// <param name="settings">Supplies default entitlements and the page size ceiling.</param>
/// <remarks>
/// Every mutation runs inside <c>CreateExecutionStrategy().ExecuteAsync</c>. Phase 2 enabled
/// <c>EnableRetryOnFailure</c>, and a retrying strategy refuses a user-initiated transaction unless
/// the whole transaction is handed to it, because it cannot replay half of one
/// (implementation.md §4.2). The bodies are therefore idempotent and nothing non-transactional —
/// no publish, no file write — happens inside them.
/// </remarks>
public sealed class LeaveService(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    INotificationPublisher publisher,
    IValidator<SubmitLeaveCommand> submitValidator,
    SctClock clock,
    IOptions<AppSettings> settings)
    : ILeaveService
{
    /// <inheritdoc/>
    /// <remarks>
    /// The overlap and balance checks are inside the transaction. Checking before opening one
    /// reintroduces exactly the race the transaction exists to prevent (spec §3.4.4).
    /// </remarks>
    public async Task<Result<Guid>> SubmitAsync(SubmitLeaveCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.EmployeeId is not { } employeeId)
        {
            return Result<Guid>.Fail(ErrorCode.Forbidden, "No employee is signed in.");
        }

        var validation = await submitValidator.ValidateAsync(command, ct).ConfigureAwait(false);

        if (!validation.IsValid)
        {
            return Result<Guid>.Fail(
                ErrorCode.Validation,
                validation.Errors[0].ErrorMessage);
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        Result<Guid> outcome = Result<Guid>.Fail(ErrorCode.BusinessRule, "Not executed.");
        IReadOnlyList<Guid> notified = [];

        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            (outcome, notified) = await SubmitCoreAsync(db, employeeId, command, ct)
                .ConfigureAwait(false);

            if (!outcome.IsSuccess)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        Signal(notified);

        return outcome;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<LeaveRequestListDto>> GetAsync(
        LeaveFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var scoped = db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ForUser(currentUser)
            .Select(e => e.Id);

        return await PageAsync(db, filter, db.LeaveRequests.Where(r => scoped.Contains(r.EmployeeId)), ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<LeaveRequestListDto>> GetOwnAsync(
        LeaveFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        if (currentUser.EmployeeId is not { } ownId)
        {
            return PagedResult<LeaveRequestListDto>.Empty(page, pageSize);
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        return await PageAsync(db, filter, db.LeaveRequests.Where(r => r.EmployeeId == ownId), ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<LeaveRequestDetailDto>> GetByIdAsync(
        Guid requestId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var scoped = db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ForUser(currentUser)
            .Select(e => e.Id);

        var request = await db.LeaveRequests
            .AsNoTracking()
            .Where(r => r.Id == requestId && scoped.Contains(r.EmployeeId))
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (request is null)
        {
            return Result<LeaveRequestDetailDto>.Fail(ErrorCode.NotFound, "Leave request not found.");
        }

        var names = await NamesForAsync(
                db,
                [request.EmployeeId, request.ReviewedById, request.CancelledById],
                ct)
            .ConfigureAwait(false);

        var summary = Summarise(request, names);
        var today = clock.Today;

        var detail = new LeaveRequestDetailDto(
            summary,
            request.Reason,
            request.ReviewedById is { } reviewer ? names.GetValueOrDefault(reviewer) : null,
            request.ReviewedAt,
            request.ReviewNote,
            request.CancelledAt,
            request.CancelledById is { } canceller ? names.GetValueOrDefault(canceller) : null,
            request.RestoredDays,
            CanCancel: CanCancel(request, today),
            CanDecide: CanDecide(request));

        return Result<LeaveRequestDetailDto>.Success(detail);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The <c>rowversion</c> on the balance is what stops two Admins approving at once from both
    /// reading the same starting figure. SQL Server maintains it, so nothing here increments
    /// anything (architecture.md §4.7).
    /// </remarks>
    public Task<Result> ApproveAsync(ApproveLeaveCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        return DecideAsync(command.RequestId, LeaveStatus.Approved, command.Note, ct);
    }

    /// <inheritdoc/>
    public Task<Result> RejectAsync(RejectLeaveCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        return DecideAsync(command.RequestId, LeaveStatus.Rejected, command.Note, ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Cancelling mid-leave restores only the business days from today forward. v2.0 restored the
    /// full amount, crediting employees for leave they had already taken (spec §3.4.5).
    /// </remarks>
    public async Task<Result> CancelAsync(CancelLeaveCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.EmployeeId is not { } actorId)
        {
            return Result.Fail(ErrorCode.Forbidden, "No employee is signed in.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var outcome = Result.Fail(ErrorCode.BusinessRule, "Not executed.");
        IReadOnlyList<Guid> notified = [];

        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            (outcome, notified) = await CancelCoreAsync(db, actorId, command, ct).ConfigureAwait(false);

            if (!outcome.IsSuccess)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return;
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                outcome = Result.Fail(
                    ErrorCode.ConcurrencyConflict,
                    "Someone else changed this request. Please retry.");
            }
        }).ConfigureAwait(false);

        Signal(notified);

        return outcome;
    }

    private async Task<(Result<Guid> Outcome, IReadOnlyList<Guid> Notified)> SubmitCoreAsync(
        IApplicationDbContext db,
        Guid employeeId,
        SubmitLeaveCommand command,
        CancellationToken ct)
    {
        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == employeeId, ct)
            .ConfigureAwait(false);

        // The filter already excludes inactive employees, so a miss means inactive or gone.
        if (employee is null)
        {
            return (Result<Guid>.Fail(ErrorCode.Forbidden, "Only an active employee may request leave."), []);
        }

        var today = clock.Today;

        if (employee.IsInProbation(today, settings.Value.ProbationMonths))
        {
            return (Result<Guid>.Fail(
                ErrorCode.BusinessRule,
                "Leave cannot be requested during the probation period."), []);
        }

        // PeriodFor throws when the date precedes the hire date, and a future-dated hire can
        // legitimately submit for a date before they start. That is an outcome, not a defect.
        if (command.StartDate < employee.HireDate)
        {
            return (Result<Guid>.Fail(
                ErrorCode.BusinessRule,
                "Leave cannot start before the hire date."), []);
        }

        var (periodStart, periodEnd) = employee.PeriodFor(command.StartDate);

        if (command.EndDate > periodEnd)
        {
            return (Result<Guid>.Fail(
                ErrorCode.BusinessRule,
                "The range crosses a balance reset. Submit one request either side of the anniversary."), []);
        }

        var holidays = (await db.PublicHolidays
                .Where(h => h.Date >= command.StartDate && h.Date <= command.EndDate)
                .Select(h => h.Date)
                .ToListAsync(ct)
                .ConfigureAwait(false))
            .ToHashSet();

        var businessDays = BusinessDayRules.Count(command.StartDate, command.EndDate, holidays);

        if (businessDays < 1)
        {
            return (Result<Guid>.Fail(
                ErrorCode.BusinessRule,
                "The range contains no business days."), []);
        }

        // Inside the transaction on purpose: this is a read followed by a write (spec §3.4.4).
        var overlaps = await db.LeaveRequests
            .AnyAsync(
                r => r.EmployeeId == employeeId
                     && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
                     && r.StartDate <= command.EndDate
                     && r.EndDate >= command.StartDate,
                ct)
            .ConfigureAwait(false);

        if (overlaps)
        {
            return (Result<Guid>.Fail(
                ErrorCode.Conflict,
                "That range overlaps leave you have already requested."), []);
        }

        var balances = await LeaveBalanceAccessor
            .EnsureCurrentPeriodAsync(db, employee, today, settings.Value.DefaultLeaveEntitlements, ct)
            .ConfigureAwait(false);

        if (command.LeaveType != LeaveType.Unpaid)
        {
            var balance = LeaveBalanceAccessor.For(balances, command.LeaveType, command.StartDate);

            if (balance is null)
            {
                return (Result<Guid>.Fail(
                    ErrorCode.BusinessRule,
                    $"You have no {command.LeaveType} balance for that period."), []);
            }

            if (balance.Remaining < businessDays)
            {
                return (Result<Guid>.Fail(
                    ErrorCode.BusinessRule,
                    $"That request needs {businessDays} days and {balance.Remaining} remain."), []);
            }
        }

        var request = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = command.LeaveType,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            BusinessDays = businessDays,
            Reason = command.Reason,
            Status = LeaveStatus.Pending,
        };

        db.LeaveRequests.Add(request);

        var notified = await NotificationWriter
            .StageForAdminsAsync(
                db,
                NotificationMessages.LeaveSubmittedTitle,
                NotificationMessages.LeaveSubmitted(
                    $"{employee.FirstName} {employee.LastName}",
                    command.LeaveType,
                    NotificationMessages.DateRange(command.StartDate, command.EndDate)),
                "/leave/manage",
                ct)
            .ConfigureAwait(false);

        // periodStart is unused beyond the boundary check; the balance is selected by start date.
        _ = periodStart;

        return (Result<Guid>.Success(request.Id), notified);
    }

    private async Task<Result> DecideAsync(
        Guid requestId,
        LeaveStatus decision,
        string? note,
        CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } reviewerId)
        {
            return Result.Fail(ErrorCode.Forbidden, "No employee is signed in.");
        }

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may decide leave.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var outcome = Result.Fail(ErrorCode.BusinessRule, "Not executed.");
        IReadOnlyList<Guid> notified = [];

        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            (outcome, notified) = await DecideCoreAsync(db, reviewerId, requestId, decision, note, ct)
                .ConfigureAwait(false);

            if (!outcome.IsSuccess)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return;
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Two Admins deciding at once: one wins, the other is told to look again rather
                // than silently overdrawing the balance (architecture.md §4.7).
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                outcome = Result.Fail(
                    ErrorCode.ConcurrencyConflict,
                    "Someone else decided this request. Please retry.");
            }
        }).ConfigureAwait(false);

        Signal(notified);

        return outcome;
    }

    private async Task<(Result Outcome, IReadOnlyList<Guid> Notified)> DecideCoreAsync(
        IApplicationDbContext db,
        Guid reviewerId,
        Guid requestId,
        LeaveStatus decision,
        string? note,
        CancellationToken ct)
    {
        var request = await db.LeaveRequests
            .SingleOrDefaultAsync(r => r.Id == requestId, ct)
            .ConfigureAwait(false);

        if (request is null)
        {
            return (Result.Fail(ErrorCode.NotFound, "Leave request not found."), []);
        }

        // Separation of duties, enforced here rather than in the UI (spec §3.4.6).
        if (request.EmployeeId == reviewerId)
        {
            return (Result.Fail(
                ErrorCode.Forbidden,
                "An administrator cannot decide their own leave request. Another administrator must act on it."), []);
        }

        if (request.Status != LeaveStatus.Pending)
        {
            return (Result.Fail(
                ErrorCode.Conflict,
                $"That request is already {request.Status}."), []);
        }

        var employee = await db.Employees
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return (Result.Fail(ErrorCode.NotFound, "Employee not found."), []);
        }

        if (decision == LeaveStatus.Approved && request.LeaveType != LeaveType.Unpaid)
        {
            var balances = await LeaveBalanceAccessor
                .EnsureCurrentPeriodAsync(db, employee, clock.Today, settings.Value.DefaultLeaveEntitlements, ct)
                .ConfigureAwait(false);

            var balance = LeaveBalanceAccessor.For(balances, request.LeaveType, request.StartDate);

            if (balance is null)
            {
                return (Result.Fail(
                    ErrorCode.BusinessRule,
                    "The employee has no balance for that period."), []);
            }

            if (balance.Remaining < request.BusinessDays)
            {
                return (Result.Fail(
                    ErrorCode.BusinessRule,
                    $"Approving needs {request.BusinessDays} days and {balance.Remaining} remain."), []);
            }

            balance.Used += request.BusinessDays;
        }

        request.Status = decision;
        request.ReviewedById = reviewerId;
        request.ReviewedAt = clock.UtcNow.UtcDateTime;
        request.ReviewNote = note;

        var dates = NotificationMessages.DateRange(request.StartDate, request.EndDate);

        var message = decision == LeaveStatus.Approved
            ? NotificationMessages.LeaveApproved(request.LeaveType, dates)
            : NotificationMessages.LeaveRejected(request.LeaveType, dates, note);

        NotificationWriter.Stage(
            db,
            request.EmployeeId,
            decision == LeaveStatus.Approved
                ? NotificationMessages.LeaveApprovedTitle
                : NotificationMessages.LeaveRejectedTitle,
            message,
            $"/leave/{request.Id}");

        return (Result.Success(), [request.EmployeeId]);
    }

    private async Task<(Result Outcome, IReadOnlyList<Guid> Notified)> CancelCoreAsync(
        IApplicationDbContext db,
        Guid actorId,
        CancelLeaveCommand command,
        CancellationToken ct)
    {
        var request = await db.LeaveRequests
            .SingleOrDefaultAsync(r => r.Id == command.RequestId, ct)
            .ConfigureAwait(false);

        if (request is null)
        {
            return (Result.Fail(ErrorCode.NotFound, "Leave request not found."), []);
        }

        var today = clock.Today;
        var isAdmin = currentUser.IsAdmin;
        var isOwner = request.EmployeeId == actorId;

        if (!isAdmin && !isOwner)
        {
            return (Result.Fail(ErrorCode.NotFound, "Leave request not found."), []);
        }

        if (request.Status is LeaveStatus.Cancelled or LeaveStatus.Rejected)
        {
            return (Result.Fail(ErrorCode.Conflict, $"That request is already {request.Status}."), []);
        }

        // An employee may withdraw a Pending request at any time, but an Approved one only before
        // it starts. An Admin may cancel either, at any time (spec §3.4.5).
        if (!isAdmin && request.Status == LeaveStatus.Approved && request.StartDate <= today)
        {
            return (Result.Fail(
                ErrorCode.BusinessRule,
                "Leave that has already started can only be cancelled by an administrator."), []);
        }

        var employee = await db.Employees
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return (Result.Fail(ErrorCode.NotFound, "Employee not found."), []);
        }

        var restored = 0;

        if (request.Status == LeaveStatus.Approved && request.LeaveType != LeaveType.Unpaid)
        {
            var balances = await LeaveBalanceAccessor
                .EnsureCurrentPeriodAsync(db, employee, today, settings.Value.DefaultLeaveEntitlements, ct)
                .ConfigureAwait(false);

            var balance = LeaveBalanceAccessor.For(balances, request.LeaveType, request.StartDate);

            if (balance is not null)
            {
                restored = await RestorableDaysAsync(db, request, today, ct).ConfigureAwait(false);

                // Never below zero: an adjustment could have moved Used since approval.
                balance.Used = Math.Max(0, balance.Used - restored);
            }
        }

        request.Status = LeaveStatus.Cancelled;
        request.CancelledAt = clock.UtcNow.UtcDateTime;
        request.CancelledById = actorId;
        request.RestoredDays = restored;

        if (!string.IsNullOrWhiteSpace(command.Note))
        {
            request.ReviewNote = command.Note;
        }

        var dates = NotificationMessages.DateRange(request.StartDate, request.EndDate);

        // The counterparty is told: an Admin cancelling notifies the employee, an employee
        // cancelling notifies every Admin (spec §3.9.1).
        if (isOwner)
        {
            var recipients = await NotificationWriter
                .StageForAdminsAsync(
                    db,
                    NotificationMessages.LeaveCancelledTitle,
                    NotificationMessages.LeaveCancelledByEmployee(
                        $"{employee.FirstName} {employee.LastName}",
                        request.LeaveType,
                        dates),
                    "/leave/manage",
                    ct)
                .ConfigureAwait(false);

            return (Result.Success(), recipients);
        }

        NotificationWriter.Stage(
            db,
            request.EmployeeId,
            NotificationMessages.LeaveCancelledTitle,
            NotificationMessages.LeaveCancelledByAdmin(request.LeaveType, dates),
            $"/leave/{request.Id}");

        return (Result.Success(), [request.EmployeeId]);
    }

    /// <summary>
    /// Loads the holidays the restore arithmetic needs, then applies
    /// <see cref="LeaveCancellationRules"/> to it.
    /// </summary>
    private static async Task<int> RestorableDaysAsync(
        IApplicationDbContext db,
        LeaveRequest request,
        DateOnly today,
        CancellationToken ct)
    {
        var holidays = (await db.PublicHolidays
                .Where(h => h.Date >= today && h.Date <= request.EndDate)
                .Select(h => h.Date)
                .ToListAsync(ct)
                .ConfigureAwait(false))
            .ToHashSet();

        return LeaveCancellationRules.RestorableDays(
            request.StartDate,
            request.EndDate,
            request.BusinessDays,
            today,
            holidays);
    }

    private async Task<PagedResult<LeaveRequestListDto>> PageAsync(
        IApplicationDbContext db,
        LeaveFilter filter,
        IQueryable<LeaveRequest> query,
        CancellationToken ct)
    {
        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        query = query.AsNoTracking();

        if (filter.From is { } from)
        {
            query = query.Where(r => r.EndDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(r => r.StartDate <= to);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            var inDepartment = db.Employees
                .IgnoreQueryFilters()
                .Where(e => e.DepartmentId == departmentId)
                .Select(e => e.Id);

            query = query.Where(r => inDepartment.Contains(r.EmployeeId));
        }

        if (filter.EmployeeId is { } employeeId)
        {
            query = query.Where(r => r.EmployeeId == employeeId);
        }

        if (filter.LeaveType is { } leaveType)
        {
            query = query.Where(r => r.LeaveType == leaveType);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var requests = await query
            .ApplySort(filter.SortBy, filter.SortDescending)
            .Skip(PageRequestExtensions.SkipFor(page, pageSize))
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var names = await NamesForAsync(db, [.. requests.Select(r => (Guid?)r.EmployeeId)], ct)
            .ConfigureAwait(false);

        IReadOnlyList<LeaveRequestListDto> items =
            [.. requests.Select(request => Summarise(request, names))];

        return new PagedResult<LeaveRequestListDto>(items, total, page, pageSize);
    }

    /// <remarks>
    /// Names are looked up separately rather than joined: the employee navigation is required and
    /// filtered, so joining through it drops every row belonging to a departed employee (ADR-0012).
    /// </remarks>
    private static async Task<Dictionary<Guid, string>> NamesForAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid?> ids,
        CancellationToken ct)
    {
        var wanted = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();

        if (wanted.Length == 0)
        {
            return [];
        }

        return await db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => wanted.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct)
            .ConfigureAwait(false);
    }

    private static LeaveRequestListDto Summarise(
        LeaveRequest request,
        Dictionary<Guid, string> names) =>
        new(
            request.Id,
            request.EmployeeId,
            names.TryGetValue(request.EmployeeId, out var name) ? name : string.Empty,
            request.LeaveType,
            request.StartDate,
            request.EndDate,
            request.BusinessDays,
            request.Status,
            request.CreatedAt);

    private bool CanCancel(LeaveRequest request, DateOnly today) =>
        request.Status switch
        {
            LeaveStatus.Pending => currentUser.IsAdmin || request.EmployeeId == currentUser.EmployeeId,
            LeaveStatus.Approved => currentUser.IsAdmin
                                    || (request.EmployeeId == currentUser.EmployeeId
                                        && request.StartDate > today),
            _ => false,
        };

    private bool CanDecide(LeaveRequest request) =>
        request.Status == LeaveStatus.Pending
        && currentUser.IsAdmin
        && request.EmployeeId != currentUser.EmployeeId;

    private void Signal(IReadOnlyList<Guid> recipients)
    {
        foreach (var recipient in recipients)
        {
            publisher.Publish(recipient);
        }
    }
}
