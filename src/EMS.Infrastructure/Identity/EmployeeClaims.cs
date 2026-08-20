namespace EMS.Infrastructure.Identity;

/// <summary>
/// Claim types that carry the employee identity behind an authenticated user.
/// </summary>
/// <remarks>
/// <see cref="EMS.Application.Common.Interfaces.ICurrentUser"/> is a synchronous contract, so the
/// acting employee and a Manager's
/// scope have to be on the principal rather than fetched per call. They are added at principal
/// creation by <see cref="EmployeeClaimsPrincipalFactory"/> and refreshed whenever the security
/// stamp changes, which the revalidating authentication state provider checks every 30 minutes
/// (architecture.md §3.1).
/// </remarks>
public static class EmployeeClaims
{
    /// <summary>The acting employee's identifier.</summary>
    public const string EmployeeId = "employee_id";

    /// <summary>One claim per department the user manages. Absent for non-Managers.</summary>
    public const string ManagedDepartment = "managed_department";
}
