using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Options;
using EMS.Application.Common.Security;
using EMS.Application.Notifications;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Departments;

/// <summary>Department reads and administration.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="publisher">Signals the bell after a commit.</param>
/// <param name="settings">Supplies the page size ceiling.</param>
public sealed class DepartmentService(
    IApplicationDbContextFactory factory,
    INotificationPublisher publisher,
    IOptions<AppSettings> settings)
    : IDepartmentService
{
    /// <inheritdoc/>
    public async Task<PagedResult<DepartmentListDto>> GetAsync(
        DepartmentFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        var query = db.Departments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(d => d.Name.Contains(term));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await Project(db, query.ApplySort(filter.SortBy, filter.SortDescending))
            .Skip(PageRequestExtensions.SkipFor(page, pageSize))
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<DepartmentListDto>(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DepartmentListDto>> GetAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        return await Project(db, db.Departments.AsNoTracking().OrderBy(d => d.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<DepartmentListDto>> GetByIdAsync(Guid departmentId, CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var department = await Project(db, db.Departments.AsNoTracking().Where(d => d.Id == departmentId))
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return department is null
            ? Result<DepartmentListDto>.Fail(ErrorCode.NotFound, "Department not found.")
            : Result<DepartmentListDto>.Success(department);
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateAsync(CreateDepartmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        // Name is uniquely indexed; the check is a courteous message, not the guard.
        var taken = await db.Departments
            .AnyAsync(d => d.Name == command.Name, ct)
            .ConfigureAwait(false);

        if (taken)
        {
            return Result<Guid>.Fail(ErrorCode.Conflict, "A department with that name already exists.");
        }

        if (command.ManagerId is { } managerId
            && !await IsAssignableManagerAsync(db, managerId, ct).ConfigureAwait(false))
        {
            return Result<Guid>.Fail(ErrorCode.BusinessRule, "The manager must be an active employee.");
        }

        var department = new Department
        {
            Name = command.Name,
            Description = command.Description,
            ManagerId = command.ManagerId,
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(department.Id);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(UpdateDepartmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var department = await db.Departments
            .SingleOrDefaultAsync(d => d.Id == command.DepartmentId, ct)
            .ConfigureAwait(false);

        if (department is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Department not found.");
        }

        var taken = await db.Departments
            .AnyAsync(d => d.Name == command.Name && d.Id != command.DepartmentId, ct)
            .ConfigureAwait(false);

        if (taken)
        {
            return Result.Fail(ErrorCode.Conflict, "A department with that name already exists.");
        }

        department.Name = command.Name;
        department.Description = command.Description;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Clearing or replacing a manager leaves the department unmanaged for as long as it takes an
    /// Admin to notice, so every Admin is told (spec §3.9.1).
    /// </remarks>
    public async Task<Result> AssignManagerAsync(
        AssignDepartmentManagerCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var department = await db.Departments
            .SingleOrDefaultAsync(d => d.Id == command.DepartmentId, ct)
            .ConfigureAwait(false);

        if (department is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Department not found.");
        }

        if (command.ManagerId is { } managerId
            && !await IsAssignableManagerAsync(db, managerId, ct).ConfigureAwait(false))
        {
            return Result.Fail(ErrorCode.BusinessRule, "The manager must be an active employee.");
        }

        var vacated = department.ManagerId is not null && command.ManagerId != department.ManagerId;

        department.ManagerId = command.ManagerId;

        IReadOnlyList<Guid> notified = [];

        if (vacated)
        {
            notified = await NotificationWriter
                .StageForAdminsAsync(
                    db,
                    NotificationMessages.ManagerVacatedTitle,
                    NotificationMessages.ManagerVacated(department.Name),
                    "/departments",
                    ct)
                .ConfigureAwait(false);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // After the write, never before: the publisher is not transactional.
        foreach (var recipient in notified)
        {
            publisher.Publish(recipient);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteAsync(Guid departmentId, CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var department = await db.Departments
            .SingleOrDefaultAsync(d => d.Id == departmentId, ct)
            .ConfigureAwait(false);

        if (department is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Department not found.");
        }

        // Employees carry a required department, so a populated one cannot be removed. The
        // database would refuse it too; this says why.
        var populated = await db.Employees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.DepartmentId == departmentId, ct)
            .ConfigureAwait(false);

        if (populated)
        {
            return Result.Fail(
                ErrorCode.BusinessRule,
                "Move the department's employees before deleting it.");
        }

        db.Departments.Remove(department);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <remarks>
    /// Department has no manager or employee navigation, so both are correlated subqueries. Neither
    /// ignores the query filter: an inactive employee is neither a manager nor a headcount.
    /// </remarks>
    private static IQueryable<DepartmentListDto> Project(
        IApplicationDbContext db,
        IQueryable<Department> query) =>
        query.Select(d => new DepartmentListDto(
            d.Id,
            d.Name,
            d.Description,
            d.ManagerId,
            db.Employees
                .Where(e => e.Id == d.ManagerId)
                .Select(e => e.FirstName + " " + e.LastName)
                .FirstOrDefault(),
            db.Employees.Count(e => e.DepartmentId == d.Id)));

    private static Task<bool> IsAssignableManagerAsync(
        IApplicationDbContext db,
        Guid managerId,
        CancellationToken ct) =>
        db.Employees.AnyAsync(
            e => e.Id == managerId && e.Status == EmployeeStatus.Active,
            ct);
}
