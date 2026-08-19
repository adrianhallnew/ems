using EMS.Domain.Entities;

namespace EMS.Application.Common.Security;

/// <summary>
/// The sort columns each grid may ask for, and what each one means.
/// </summary>
/// <remarks>
/// A sort column arriving from a data grid is untrusted input. Every allow-list below maps a name
/// onto a compiled expression and falls back to the entity's default order when the name is not
/// recognised. No client-supplied string is ever interpolated into a query.
/// </remarks>
public static class EmployeeSort
{
    /// <summary>Applies a requested sort, or the default when it is not recognised.</summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sortBy">The requested column name.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<Employee> ApplySort(
        this IQueryable<Employee> query,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sortBy?.ToLowerInvariant() switch
        {
            "firstname" => descending ? query.OrderByDescending(e => e.FirstName) : query.OrderBy(e => e.FirstName),
            "lastname" => descending ? query.OrderByDescending(e => e.LastName) : query.OrderBy(e => e.LastName),
            "email" => descending ? query.OrderByDescending(e => e.Email) : query.OrderBy(e => e.Email),
            "jobtitle" => descending ? query.OrderByDescending(e => e.JobTitle) : query.OrderBy(e => e.JobTitle),
            "hiredate" => descending ? query.OrderByDescending(e => e.HireDate) : query.OrderBy(e => e.HireDate),
            "role" => descending ? query.OrderByDescending(e => e.Role) : query.OrderBy(e => e.Role),
            "status" => descending ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            _ => query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName),
        };
    }
}

/// <summary>The sort columns the department grid may ask for.</summary>
public static class DepartmentSort
{
    /// <summary>Applies a requested sort, or the default when it is not recognised.</summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sortBy">The requested column name.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<Department> ApplySort(
        this IQueryable<Department> query,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sortBy?.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name),
            "createdat" => descending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt),
            _ => query.OrderBy(d => d.Name),
        };
    }
}

/// <summary>The sort columns the attendance grids may ask for.</summary>
public static class AttendanceSort
{
    /// <summary>Applies a requested sort, or the default when it is not recognised.</summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sortBy">The requested column name.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<AttendanceRecord> ApplySort(
        this IQueryable<AttendanceRecord> query,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sortBy?.ToLowerInvariant() switch
        {
            "clockin" => descending ? query.OrderByDescending(a => a.ClockIn) : query.OrderBy(a => a.ClockIn),
            "clockout" => descending ? query.OrderByDescending(a => a.ClockOut) : query.OrderBy(a => a.ClockOut),
            "workedminutes" => descending ? query.OrderByDescending(a => a.WorkedMinutes) : query.OrderBy(a => a.WorkedMinutes),
            "isflagged" => descending ? query.OrderByDescending(a => a.IsFlagged) : query.OrderBy(a => a.IsFlagged),
            "date" => descending ? query.OrderByDescending(a => a.Date) : query.OrderBy(a => a.Date),
            _ => query.OrderByDescending(a => a.Date),
        };
    }
}

/// <summary>The sort columns the leave grids may ask for.</summary>
public static class LeaveSort
{
    /// <summary>Applies a requested sort, or the default when it is not recognised.</summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sortBy">The requested column name.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<LeaveRequest> ApplySort(
        this IQueryable<LeaveRequest> query,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sortBy?.ToLowerInvariant() switch
        {
            "startdate" => descending ? query.OrderByDescending(r => r.StartDate) : query.OrderBy(r => r.StartDate),
            "enddate" => descending ? query.OrderByDescending(r => r.EndDate) : query.OrderBy(r => r.EndDate),
            "leavetype" => descending ? query.OrderByDescending(r => r.LeaveType) : query.OrderBy(r => r.LeaveType),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "businessdays" => descending ? query.OrderByDescending(r => r.BusinessDays) : query.OrderBy(r => r.BusinessDays),
            "createdat" => descending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };
    }
}

/// <summary>The sort columns the notification list may ask for.</summary>
public static class NotificationSort
{
    /// <summary>Applies a requested sort, or the default when it is not recognised.</summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sortBy">The requested column name.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<Notification> ApplySort(
        this IQueryable<Notification> query,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sortBy?.ToLowerInvariant() switch
        {
            "isread" => descending ? query.OrderByDescending(n => n.IsRead) : query.OrderBy(n => n.IsRead),
            "createdat" => descending ? query.OrderByDescending(n => n.CreatedAt) : query.OrderBy(n => n.CreatedAt),
            _ => query.OrderByDescending(n => n.CreatedAt),
        };
    }
}

/// <summary>The sort columns the audit log may ask for.</summary>
public static class AuditSort
{
    /// <summary>Applies a requested sort, or the default when it is not recognised.</summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sortBy">The requested column name.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<AuditEntry> ApplySort(
        this IQueryable<AuditEntry> query,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sortBy?.ToLowerInvariant() switch
        {
            "entitytype" => descending ? query.OrderByDescending(a => a.EntityType) : query.OrderBy(a => a.EntityType),
            "action" => descending ? query.OrderByDescending(a => a.Action) : query.OrderBy(a => a.Action),
            "actordescription" => descending ? query.OrderByDescending(a => a.ActorDescription) : query.OrderBy(a => a.ActorDescription),
            "changedat" => descending ? query.OrderByDescending(a => a.ChangedAt) : query.OrderBy(a => a.ChangedAt),
            _ => query.OrderByDescending(a => a.ChangedAt),
        };
    }
}
