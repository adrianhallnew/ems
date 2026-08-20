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

namespace EMS.Application.Employees;

/// <summary>Employee reads and administration.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">The acting user, whose scope every read applies.</param>
/// <param name="accounts">Provisions the Identity account behind an employee.</param>
/// <param name="publisher">Signals the bell after a commit.</param>
/// <param name="clock">The only source of "today".</param>
/// <param name="settings">Supplies the page size ceiling and the probation length.</param>
public sealed class EmployeeService(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    IIdentityAccountService accounts,
    INotificationPublisher publisher,
    SctClock clock,
    IOptions<AppSettings> settings)
    : IEmployeeService
{
    /// <inheritdoc/>
    public async Task<PagedResult<EmployeeListDto>> GetAsync(
        EmployeeFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        // Inactive employees are filtered out globally; seeing them is an explicit request, and
        // only an Admin may make it (spec §3.1.6, architecture.md §2.5).
        var query = filter.IncludeInactive && currentUser.IsAdmin
            ? db.Employees.IgnoreQueryFilters()
            : db.Employees;

        query = query.AsNoTracking().ForUser(currentUser);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(e =>
                e.FirstName.Contains(term)
                || e.LastName.Contains(term)
                || e.Email.Contains(term)
                || e.JobTitle.Contains(term));
        }

        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (filter.Role is { } role)
        {
            query = query.Where(e => e.Role == role);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await ProjectList(db, query.ApplySort(filter.SortBy, filter.SortDescending))
            .Skip(PageRequestExtensions.SkipFor(page, pageSize))
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<EmployeeListDto>(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Out of scope reads as not found. Returning forbidden instead would confirm the record exists
    /// (architecture.md §3.4).
    /// </remarks>
    public async Task<Result<EmployeeDetailDto>> GetByIdAsync(Guid employeeId, CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await ProjectDetail(db, db.Employees
                .AsNoTracking()
                .ForUser(currentUser)
                .Where(e => e.Id == employeeId))
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return employee is null
            ? Result<EmployeeDetailDto>.Fail(ErrorCode.NotFound, "Employee not found.")
            : Result<EmployeeDetailDto>.Success(employee);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The salary-bearing projection. Spec §2.5.6 requires the value to be absent from a non-Admin
    /// read rather than blanked in one, which is why this is a separate method and a separate DTO.
    /// </remarks>
    public async Task<Result<EmployeeAdminDetailDto>> GetForAdminAsync(
        Guid employeeId,
        CancellationToken ct)
    {
        if (!currentUser.IsAdmin)
        {
            return Result<EmployeeAdminDetailDto>.Fail(
                ErrorCode.Forbidden,
                "Only an administrator may read salary.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await db.Employees
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.Id == employeeId)
            .Select(e => new EmployeeAdminDetailDto(
                Detail(e, db, clock.Today, settings.Value.ProbationMonths),
                e.Salary,
                e.MustChangePassword,
                e.DeactivatedAt))
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return employee is null
            ? Result<EmployeeAdminDetailDto>.Fail(ErrorCode.NotFound, "Employee not found.")
            : Result<EmployeeAdminDetailDto>.Success(employee);
    }

    /// <inheritdoc/>
    public async Task<Result<EmployeeDetailDto>> GetOwnProfileAsync(CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } ownId)
        {
            return Result<EmployeeDetailDto>.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await ProjectDetail(db, db.Employees
                .AsNoTracking()
                .Where(e => e.Id == ownId))
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return employee is null
            ? Result<EmployeeDetailDto>.Fail(ErrorCode.NotFound, "Employee not found.")
            : Result<EmployeeDetailDto>.Success(employee);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The Identity account is created first: an employee row with no account is unusable, and a
    /// failed account creation must not leave one behind.
    /// </remarks>
    public async Task<Result<EmployeeCreatedDto>> CreateAsync(
        CreateEmployeeCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result<EmployeeCreatedDto>.Fail(
                ErrorCode.Forbidden,
                "Only an administrator may create an employee.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var emailTaken = await db.Employees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Email == command.Email, ct)
            .ConfigureAwait(false);

        if (emailTaken)
        {
            return Result<EmployeeCreatedDto>.Fail(
                ErrorCode.Conflict,
                "An employee with that email already exists.");
        }

        var departmentExists = await db.Departments
            .AnyAsync(d => d.Id == command.DepartmentId, ct)
            .ConfigureAwait(false);

        if (!departmentExists)
        {
            return Result<EmployeeCreatedDto>.Fail(ErrorCode.NotFound, "Department not found.");
        }

        var account = await accounts
            .CreateAccountAsync(command.Email, command.TemporaryPassword, command.Role, ct)
            .ConfigureAwait(false);

        if (!account.IsSuccess)
        {
            return Result<EmployeeCreatedDto>.Fail(
                account.Error!.Value.Code,
                account.Error!.Value.Message);
        }

        var employee = new Employee
        {
            UserId = account.Value.UserId,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email,
            Phone = command.Phone,
            DateOfBirth = command.DateOfBirth,
            Address = command.Address,
            EmergencyContactName = command.EmergencyContactName,
            EmergencyContactPhone = command.EmergencyContactPhone,
            Salary = command.Salary,
            JobTitle = command.JobTitle,
            ContractType = command.ContractType,
            DepartmentId = command.DepartmentId,
            Role = command.Role,
            HireDate = command.HireDate,
            Status = EmployeeStatus.Active,

            // The account ships with a temporary password, so the first sign-in must change it
            // (spec §3.1.3, architecture.md §3.6).
            MustChangePassword = true,
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<EmployeeCreatedDto>.Success(
            new EmployeeCreatedDto(employee.Id, account.Value.GeneratedPassword));
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(UpdateEmployeeCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may edit an employee.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == command.EmployeeId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        var departmentExists = await db.Departments
            .AnyAsync(d => d.Id == command.DepartmentId, ct)
            .ConfigureAwait(false);

        if (!departmentExists)
        {
            return Result.Fail(ErrorCode.NotFound, "Department not found.");
        }

        employee.FirstName = command.FirstName;
        employee.LastName = command.LastName;
        employee.Phone = command.Phone;
        employee.DateOfBirth = command.DateOfBirth;
        employee.Address = command.Address;
        employee.EmergencyContactName = command.EmergencyContactName;
        employee.EmergencyContactPhone = command.EmergencyContactPhone;
        employee.Salary = command.Salary;
        employee.JobTitle = command.JobTitle;
        employee.ContractType = command.ContractType;
        employee.DepartmentId = command.DepartmentId;
        employee.HireDate = command.HireDate;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Contact fields only. Everything an employee could use to change their own pay, role, or
    /// department is absent from the command rather than guarded in the body (spec §2.3).
    /// </remarks>
    public async Task<Result> UpdateOwnProfileAsync(
        UpdateOwnProfileCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.EmployeeId is not { } ownId)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == ownId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        employee.Phone = command.Phone;
        employee.Address = command.Address;
        employee.EmergencyContactName = command.EmergencyContactName;
        employee.EmergencyContactPhone = command.EmergencyContactPhone;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Deferred to Phase 6. Changing an email means changing the Identity account and refreshing
    /// the security stamp so open sessions revalidate (implementation.md §4.2, §6.3).
    /// </remarks>
    public Task<Result> ChangeEmailAsync(ChangeEmployeeEmailCommand command, CancellationToken ct) =>
        Task.FromResult(Result.Fail(
            ErrorCode.BusinessRule,
            "Email changes arrive with the Identity work in Phase 6."));

    /// <inheritdoc/>
    /// <remarks>Deferred to Phase 6, for the same reason as <see cref="ChangeEmailAsync"/>.</remarks>
    public Task<Result> ChangeOwnEmailAsync(ChangeOwnEmailCommand command, CancellationToken ct) =>
        Task.FromResult(Result.Fail(
            ErrorCode.BusinessRule,
            "Email changes arrive with the Identity work in Phase 6."));

    /// <inheritdoc/>
    /// <remarks>
    /// Deferred to Phase 6. A role change must update the Identity role and revoke sessions, or the
    /// old role survives on an open circuit (implementation.md §6.3).
    /// </remarks>
    public Task<Result> ChangeRoleAsync(ChangeEmployeeRoleCommand command, CancellationToken ct) =>
        Task.FromResult(Result.Fail(
            ErrorCode.BusinessRule,
            "Role changes arrive with the Identity work in Phase 6."));

    /// <inheritdoc/>
    /// <remarks>
    /// Soft delete: the row stays, the status changes (ADR-0011). A departing manager leaves their
    /// departments unmanaged, which every Admin is told about (spec §3.9.1).
    /// </remarks>
    public async Task<Result> DeactivateAsync(
        DeactivateEmployeeCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may deactivate an employee.");
        }

        if (currentUser.EmployeeId == command.EmployeeId)
        {
            return Result.Fail(ErrorCode.BusinessRule, "An administrator cannot deactivate themselves.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == command.EmployeeId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        employee.Status = EmployeeStatus.Inactive;
        employee.DeactivatedAt = clock.UtcNow.UtcDateTime;

        var vacated = await db.Departments
            .Where(d => d.ManagerId == employee.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var notified = new List<Guid>();

        foreach (var department in vacated)
        {
            department.ManagerId = null;

            var recipients = await NotificationWriter
                .StageForAdminsAsync(
                    db,
                    NotificationMessages.ManagerVacatedTitle,
                    NotificationMessages.ManagerVacated(department.Name),
                    "/departments",
                    ct)
                .ConfigureAwait(false);

            notified.AddRange(recipients);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var recipient in notified.Distinct())
        {
            publisher.Publish(recipient);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ReactivateAsync(
        ReactivateEmployeeCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAdmin)
        {
            return Result.Fail(ErrorCode.Forbidden, "Only an administrator may reactivate an employee.");
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        // The one place that must see past the soft-delete filter to find its subject.
        var employee = await db.Employees
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(e => e.Id == command.EmployeeId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        employee.Status = EmployeeStatus.Active;
        employee.DeactivatedAt = null;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private static IQueryable<EmployeeListDto> ProjectList(
        IApplicationDbContext db,
        IQueryable<Employee> query) =>
        query.Select(e => new EmployeeListDto(
            e.Id,
            e.FirstName + " " + e.LastName,
            e.Email,
            e.JobTitle,
            e.DepartmentId,
            db.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
            e.Role,
            e.Status,
            e.HireDate));

    private IQueryable<EmployeeDetailDto> ProjectDetail(
        IApplicationDbContext db,
        IQueryable<Employee> query) =>
        query.Select(e => Detail(e, db, clock.Today, settings.Value.ProbationMonths));

    /// <remarks>
    /// Probation is computed in the projection rather than read from the entity helper, because the
    /// query has to translate. The rule is the same one <c>Employee.IsInProbation</c> applies.
    /// </remarks>
    private static EmployeeDetailDto Detail(
        Employee e,
        IApplicationDbContext db,
        DateOnly today,
        int probationMonths) =>
        new(
            e.Id,
            e.FirstName,
            e.LastName,
            e.Email,
            e.Phone,
            e.DateOfBirth,
            e.Address,
            e.EmergencyContactName,
            e.EmergencyContactPhone,
            e.JobTitle,
            e.ContractType,
            e.DepartmentId,
            db.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
            e.Role,
            e.HireDate,
            e.Status,
            e.HireDate.AddMonths(probationMonths) > today);
}
