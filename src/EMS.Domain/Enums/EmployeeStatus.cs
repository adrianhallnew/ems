namespace EMS.Domain.Enums;

/// <summary>
/// Lifecycle status of an employee record. Deletion is always soft.
/// </summary>
public enum EmployeeStatus
{
    /// <summary>Employed and able to sign in.</summary>
    Active,

    /// <summary>Soft deleted. Historical data is preserved; sign-in is refused.</summary>
    Inactive,
}
