namespace EMS.Domain.Enums;

/// <summary>
/// Role an employee holds in the system.
/// </summary>
/// <remarks>
/// Identity role membership is authoritative. The value stored on <c>Employee</c> is a
/// projection kept for querying and reporting; authorisation never reads it.
/// </remarks>
public enum EmployeeRole
{
    /// <summary>Full system access.</summary>
    Admin,

    /// <summary>Read-only access scoped to the departments they manage.</summary>
    Manager,

    /// <summary>Self-service access to own data only.</summary>
    Employee,
}
