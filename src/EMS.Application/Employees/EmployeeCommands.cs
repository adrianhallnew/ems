using EMS.Domain.Enums;

namespace EMS.Application.Employees;

/// <summary>Creates an employee and its Identity account.</summary>
/// <param name="FirstName">The given name.</param>
/// <param name="LastName">The family name.</param>
/// <param name="Email">The login email, which Identity owns.</param>
/// <param name="Phone">The contact number.</param>
/// <param name="DateOfBirth">The date of birth.</param>
/// <param name="Address">The home address.</param>
/// <param name="EmergencyContactName">The emergency contact's name.</param>
/// <param name="EmergencyContactPhone">The emergency contact's number.</param>
/// <param name="Salary">The monthly salary in SCR.</param>
/// <param name="JobTitle">The job title.</param>
/// <param name="ContractType">The contract type.</param>
/// <param name="DepartmentId">The department the employee belongs to.</param>
/// <param name="Role">The role to grant in Identity and project onto the employee.</param>
/// <param name="HireDate">The hire date, which anchors probation and balance periods.</param>
/// <param name="TemporaryPassword">
/// The initial password. Null asks the service to generate one and return it once.
/// </param>
public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    DateOnly DateOfBirth,
    string Address,
    string EmergencyContactName,
    string EmergencyContactPhone,
    decimal Salary,
    string JobTitle,
    ContractType ContractType,
    Guid DepartmentId,
    EmployeeRole Role,
    DateOnly HireDate,
    string? TemporaryPassword);

/// <summary>Updates an employee's full record. Admin only.</summary>
/// <param name="EmployeeId">The employee to update.</param>
/// <param name="FirstName">The given name.</param>
/// <param name="LastName">The family name.</param>
/// <param name="Phone">The contact number.</param>
/// <param name="DateOfBirth">The date of birth.</param>
/// <param name="Address">The home address.</param>
/// <param name="EmergencyContactName">The emergency contact's name.</param>
/// <param name="EmergencyContactPhone">The emergency contact's number.</param>
/// <param name="Salary">The monthly salary in SCR.</param>
/// <param name="JobTitle">The job title.</param>
/// <param name="ContractType">The contract type.</param>
/// <param name="DepartmentId">The department the employee belongs to.</param>
/// <param name="HireDate">The hire date.</param>
/// <remarks>Email and role changes have their own commands, because both touch Identity.</remarks>
public sealed record UpdateEmployeeCommand(
    Guid EmployeeId,
    string FirstName,
    string LastName,
    string Phone,
    DateOnly DateOfBirth,
    string Address,
    string EmergencyContactName,
    string EmergencyContactPhone,
    decimal Salary,
    string JobTitle,
    ContractType ContractType,
    Guid DepartmentId,
    DateOnly HireDate);

/// <summary>Updates the acting employee's own contact fields.</summary>
/// <param name="Phone">The contact number.</param>
/// <param name="Address">The home address.</param>
/// <param name="EmergencyContactName">The emergency contact's name.</param>
/// <param name="EmergencyContactPhone">The emergency contact's number.</param>
/// <remarks>
/// No employee identifier: the acting employee comes from the authenticated principal. Spec
/// section 3.1.2 limits self-service to exactly these fields plus email, which has its own command.
/// </remarks>
public sealed record UpdateOwnProfileCommand(
    string Phone,
    string Address,
    string EmergencyContactName,
    string EmergencyContactPhone);

/// <summary>Changes an employee's login email in Identity and in the projection.</summary>
/// <param name="EmployeeId">The employee whose email changes.</param>
/// <param name="NewEmail">The new address.</param>
/// <remarks>
/// Both writes occur in one transaction and the change signs the user out of every session.
/// </remarks>
public sealed record ChangeEmployeeEmailCommand(Guid EmployeeId, string NewEmail);

/// <summary>Changes the acting employee's own login email.</summary>
/// <param name="NewEmail">The new address.</param>
public sealed record ChangeOwnEmailCommand(string NewEmail);

/// <summary>Moves an employee between Identity roles.</summary>
/// <param name="EmployeeId">The employee whose role changes.</param>
/// <param name="Role">The new role.</param>
/// <remarks>
/// Refreshes the security stamp, so elevated privileges cannot outlive the change. Refused when it
/// would leave no active Admin (spec section 3.1.2).
/// </remarks>
public sealed record ChangeEmployeeRoleCommand(Guid EmployeeId, EmployeeRole Role);

/// <summary>Deactivates an employee. Soft delete only.</summary>
/// <param name="EmployeeId">The employee to deactivate.</param>
/// <param name="Reason">The reason, recorded in the audit trail.</param>
public sealed record DeactivateEmployeeCommand(Guid EmployeeId, string Reason);

/// <summary>Returns a deactivated employee to Active.</summary>
/// <param name="EmployeeId">The employee to reactivate.</param>
public sealed record ReactivateEmployeeCommand(Guid EmployeeId);
