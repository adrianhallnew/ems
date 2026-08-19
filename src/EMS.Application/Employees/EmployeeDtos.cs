using EMS.Application.Common.Models;
using EMS.Domain.Enums;

namespace EMS.Application.Employees;

/// <summary>One row of the employee grid.</summary>
/// <param name="Id">The employee key.</param>
/// <param name="FullName">Given and family name.</param>
/// <param name="Email">The login email.</param>
/// <param name="JobTitle">The job title.</param>
/// <param name="DepartmentId">The department key.</param>
/// <param name="DepartmentName">The department name.</param>
/// <param name="Role">The role projected from Identity.</param>
/// <param name="Status">Active or Inactive.</param>
/// <param name="HireDate">The hire date.</param>
public sealed record EmployeeListDto(
    Guid Id,
    string FullName,
    string Email,
    string JobTitle,
    Guid DepartmentId,
    string DepartmentName,
    EmployeeRole Role,
    EmployeeStatus Status,
    DateOnly HireDate);

/// <summary>
/// An employee profile as seen by a Manager, or by the employee themselves.
/// </summary>
/// <param name="Id">The employee key.</param>
/// <param name="FirstName">The given name.</param>
/// <param name="LastName">The family name.</param>
/// <param name="Email">The login email.</param>
/// <param name="Phone">The contact number.</param>
/// <param name="DateOfBirth">The date of birth.</param>
/// <param name="Address">The home address.</param>
/// <param name="EmergencyContactName">The emergency contact's name.</param>
/// <param name="EmergencyContactPhone">The emergency contact's number.</param>
/// <param name="JobTitle">The job title.</param>
/// <param name="ContractType">The contract type.</param>
/// <param name="DepartmentId">The department key.</param>
/// <param name="DepartmentName">The department name.</param>
/// <param name="Role">The role projected from Identity.</param>
/// <param name="HireDate">The hire date.</param>
/// <param name="Status">Active or Inactive.</param>
/// <param name="IsInProbation">Whether probation is still running.</param>
/// <remarks>
/// Salary is absent from this type, not blanked in it. Spec section 2.5.6 requires the value never
/// to reach a non-Admin projection at all.
/// </remarks>
public sealed record EmployeeDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    DateOnly DateOfBirth,
    string Address,
    string EmergencyContactName,
    string EmergencyContactPhone,
    string JobTitle,
    ContractType ContractType,
    Guid DepartmentId,
    string DepartmentName,
    EmployeeRole Role,
    DateOnly HireDate,
    EmployeeStatus Status,
    bool IsInProbation);

/// <summary>The same profile with the Admin-only fields.</summary>
/// <param name="Profile">The fields every permitted reader sees.</param>
/// <param name="Salary">The monthly salary in SCR.</param>
/// <param name="MustChangePassword">Whether a forced password change is pending.</param>
/// <param name="DeactivatedAt">When the employee was deactivated, or null while active.</param>
public sealed record EmployeeAdminDetailDto(
    EmployeeDetailDto Profile,
    decimal Salary,
    bool MustChangePassword,
    DateTime? DeactivatedAt);

/// <summary>The outcome of provisioning an employee.</summary>
/// <param name="EmployeeId">The new employee's key.</param>
/// <param name="GeneratedPassword">
/// The password the system generated, shown to the Admin exactly once. Null when the Admin
/// supplied one.
/// </param>
public sealed record EmployeeCreatedDto(Guid EmployeeId, string? GeneratedPassword);

/// <summary>The filter behind the employee grid.</summary>
/// <remarks>
/// A Manager's query is narrowed to their departments before any identifier lookup, so an
/// out-of-scope employee is not merely hidden from the grid but unreachable by key.
/// </remarks>
public sealed record EmployeeFilter : PageRequest
{
    /// <summary>Gets a free-text term matched against name, email and job title.</summary>
    public string? Search { get; init; }

    /// <summary>Gets the department to restrict to, or null for every department in scope.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>Gets the role to restrict to.</summary>
    public EmployeeRole? Role { get; init; }

    /// <summary>Gets the status to restrict to.</summary>
    public EmployeeStatus? Status { get; init; }

    /// <summary>
    /// Gets a value indicating whether inactive employees are included.
    /// </summary>
    /// <remarks>
    /// Requires the caller to opt out of the global query filter, which only reports, audit views
    /// and department deletion checks legitimately do.
    /// </remarks>
    public bool IncludeInactive { get; init; }
}
