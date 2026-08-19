using EMS.Application.Common.Models;

namespace EMS.Application.Departments;

/// <summary>One row of the department grid.</summary>
/// <param name="Id">The department key.</param>
/// <param name="Name">The department name.</param>
/// <param name="Description">The description, if any.</param>
/// <param name="ManagerId">The assigned manager, or null.</param>
/// <param name="ManagerName">The assigned manager's name, or null.</param>
/// <param name="EmployeeCount">Active employees in the department.</param>
public sealed record DepartmentListDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ManagerId,
    string? ManagerName,
    int EmployeeCount);

/// <summary>The filter behind the department grid.</summary>
public sealed record DepartmentFilter : PageRequest
{
    /// <summary>Gets a free-text term matched against name and description.</summary>
    public string? Search { get; init; }
}

/// <summary>Creates a department.</summary>
/// <param name="Name">The unique department name.</param>
/// <param name="Description">An optional description.</param>
/// <param name="ManagerId">An optional manager, who must be an active Manager or Admin.</param>
public sealed record CreateDepartmentCommand(string Name, string? Description, Guid? ManagerId);

/// <summary>Updates a department.</summary>
/// <param name="DepartmentId">The department to update.</param>
/// <param name="Name">The unique department name.</param>
/// <param name="Description">An optional description.</param>
public sealed record UpdateDepartmentCommand(Guid DepartmentId, string Name, string? Description);

/// <summary>Assigns or clears a department's manager.</summary>
/// <param name="DepartmentId">The department.</param>
/// <param name="ManagerId">The manager to assign, or null to leave the department unmanaged.</param>
/// <remarks>
/// The manager need not belong to the department they manage (spec section 3.2.2).
/// </remarks>
public sealed record AssignDepartmentManagerCommand(Guid DepartmentId, Guid? ManagerId);
