using EMS.Application.Common.Models;

namespace EMS.Application.Departments;

/// <summary>Department reads and writes. Writes are Admin only.</summary>
public interface IDepartmentService
{
    /// <summary>Lists departments the caller may see.</summary>
    /// <param name="filter">Paging, sorting and filtering.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of departments.</returns>
    Task<PagedResult<DepartmentListDto>> GetAsync(DepartmentFilter filter, CancellationToken ct);

    /// <summary>Lists every department in scope, for dropdowns.</summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The departments.</returns>
    Task<IReadOnlyList<DepartmentListDto>> GetAllAsync(CancellationToken ct);

    /// <summary>Reads one department.</summary>
    /// <param name="departmentId">The department to read.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The department, or NotFound when out of scope.</returns>
    Task<Result<DepartmentListDto>> GetByIdAsync(Guid departmentId, CancellationToken ct);

    /// <summary>Creates a department.</summary>
    /// <param name="command">The new department's fields.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The new key.</returns>
    Task<Result<Guid>> CreateAsync(CreateDepartmentCommand command, CancellationToken ct);

    /// <summary>Updates a department.</summary>
    /// <param name="command">The updated fields.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> UpdateAsync(UpdateDepartmentCommand command, CancellationToken ct);

    /// <summary>Assigns or clears a department's manager.</summary>
    /// <param name="command">The department and the manager.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> AssignManagerAsync(AssignDepartmentManagerCommand command, CancellationToken ct);

    /// <summary>Deletes a department.</summary>
    /// <param name="departmentId">The department to delete.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>
    /// The outcome, refused while any employee is still assigned — inactive employees included,
    /// since their historical records still reference the department (spec section 3.2.2).
    /// </returns>
    Task<Result> DeleteAsync(Guid departmentId, CancellationToken ct);
}
