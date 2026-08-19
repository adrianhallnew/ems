namespace EMS.Application.Common.Interfaces;

/// <summary>
/// The employee acting in the current request, as seen by the application layer.
/// </summary>
/// <remarks>
/// Every service reads the acting employee from here rather than accepting an identifier as a
/// parameter, which makes acting-on-someone-else's-data unrepresentable rather than merely
/// discouraged. See architecture.md section 3.4.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    /// Gets the acting employee's key, or null when no user is present.
    /// </summary>
    /// <remarks>
    /// Null is expected and legitimate: background jobs, the seeder, and startup migrations all
    /// write data with no signed-in user.
    /// </remarks>
    Guid? EmployeeId { get; }

    /// <summary>Gets the acting user's email, or null when no user is present.</summary>
    string? Email { get; }

    /// <summary>Gets a value indicating whether the acting user holds the Admin role.</summary>
    bool IsAdmin { get; }

    /// <summary>Gets a value indicating whether the acting user holds the Manager role.</summary>
    bool IsManager { get; }

    /// <summary>
    /// Gets the departments a Manager manages, which is empty for every other role.
    /// </summary>
    IReadOnlySet<Guid> ManagedDepartmentIds { get; }

    /// <summary>
    /// Gets a human-readable description of the actor for the audit trail, such as the acting
    /// user's email or a system label.
    /// </summary>
    string ActorDescription { get; }
}
