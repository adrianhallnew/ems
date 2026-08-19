using EMS.Application.Common.Models;

namespace EMS.Application.Employees;

/// <summary>Employee reads and writes, scoped to the acting user.</summary>
/// <remarks>
/// Every read applies a scope predicate derived from <c>ICurrentUser</c>. A scoped lookup for an
/// out-of-scope identifier returns <see cref="ErrorCode.NotFound"/> rather than
/// <see cref="ErrorCode.Forbidden"/>, because distinguishing the two confirms the record exists.
/// </remarks>
public interface IEmployeeService
{
    /// <summary>Lists employees the caller may see.</summary>
    /// <param name="filter">Paging, sorting and filtering.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of employees.</returns>
    Task<PagedResult<EmployeeListDto>> GetAsync(EmployeeFilter filter, CancellationToken ct);

    /// <summary>Reads one employee's profile, without salary.</summary>
    /// <param name="employeeId">The employee to read.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The profile, or NotFound when out of scope.</returns>
    Task<Result<EmployeeDetailDto>> GetByIdAsync(Guid employeeId, CancellationToken ct);

    /// <summary>Reads one employee's profile including the Admin-only fields.</summary>
    /// <param name="employeeId">The employee to read.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The profile, or Forbidden for a non-Admin caller.</returns>
    Task<Result<EmployeeAdminDetailDto>> GetForAdminAsync(Guid employeeId, CancellationToken ct);

    /// <summary>Reads the acting employee's own profile.</summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The profile.</returns>
    Task<Result<EmployeeDetailDto>> GetOwnProfileAsync(CancellationToken ct);

    /// <summary>Provisions an employee and its Identity account.</summary>
    /// <param name="command">The new employee's fields.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The new key, and the generated password when the Admin supplied none.</returns>
    Task<Result<EmployeeCreatedDto>> CreateAsync(CreateEmployeeCommand command, CancellationToken ct);

    /// <summary>Updates an employee's record. Admin only.</summary>
    /// <param name="command">The updated fields.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> UpdateAsync(UpdateEmployeeCommand command, CancellationToken ct);

    /// <summary>Updates the acting employee's own contact fields.</summary>
    /// <param name="command">The updated fields.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> UpdateOwnProfileAsync(UpdateOwnProfileCommand command, CancellationToken ct);

    /// <summary>Changes an employee's login email. Admin only.</summary>
    /// <param name="command">The employee and the new address.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ChangeEmailAsync(ChangeEmployeeEmailCommand command, CancellationToken ct);

    /// <summary>Changes the acting employee's own login email.</summary>
    /// <param name="command">The new address.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ChangeOwnEmailAsync(ChangeOwnEmailCommand command, CancellationToken ct);

    /// <summary>Moves an employee between roles. Admin only.</summary>
    /// <param name="command">The employee and the new role.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome, refusing the change when it would leave no active Admin.</returns>
    Task<Result> ChangeRoleAsync(ChangeEmployeeRoleCommand command, CancellationToken ct);

    /// <summary>Deactivates an employee. Admin only.</summary>
    /// <param name="command">The employee and the reason.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome, refusing the change when it would leave no active Admin.</returns>
    Task<Result> DeactivateAsync(DeactivateEmployeeCommand command, CancellationToken ct);

    /// <summary>Returns a deactivated employee to Active. Admin only.</summary>
    /// <param name="command">The employee.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ReactivateAsync(ReactivateEmployeeCommand command, CancellationToken ct);
}
