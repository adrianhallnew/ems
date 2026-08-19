using EMS.Application.Common.Interfaces;
using EMS.Domain.Entities;

namespace EMS.Application.Common.Security;

/// <summary>Narrows a query to the rows the acting user may see.</summary>
/// <remarks>
/// Role policies answer "may this user reach this page". They do not answer "which rows may this
/// user see" — that is answered here, as a query predicate, before any identifier lookup. A scoped
/// lookup that finds nothing returns NotFound rather than Forbidden, because distinguishing the two
/// confirms the record exists (spec section 2.5.4).
/// </remarks>
public static class EmployeeScope
{
    /// <summary>Restricts an employee query to the acting user's scope.</summary>
    /// <param name="query">The query to narrow.</param>
    /// <param name="user">The acting user.</param>
    /// <returns>The narrowed query.</returns>
    /// <remarks>
    /// An Admin sees everyone. A Manager sees the employees of the departments they manage, plus
    /// their own record. Everyone else sees only their own record. A Manager with no assigned
    /// department therefore has an empty scope beyond self (spec section 2.4).
    /// </remarks>
    public static IQueryable<Employee> ForUser(this IQueryable<Employee> query, ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsAdmin)
        {
            return query;
        }

        var ownId = user.EmployeeId;

        if (user.IsManager)
        {
            // Materialised to an array so the provider translates this to an IN clause.
            var departmentIds = user.ManagedDepartmentIds.ToArray();

            return query.Where(e => departmentIds.Contains(e.DepartmentId) || e.Id == ownId);
        }

        return query.Where(e => e.Id == ownId);
    }

    /// <summary>Reports whether the acting user may see salary values at all.</summary>
    /// <param name="user">The acting user.</param>
    /// <returns><c>true</c> for an Admin.</returns>
    /// <remarks>
    /// Callers use this to choose a projection, not to blank a field. Spec section 2.5.6 requires
    /// the value never to reach a non-Admin projection.
    /// </remarks>
    public static bool CanSeeSalary(ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsAdmin;
    }

    /// <summary>Reports whether the acting user may act on another employee's record.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="employeeId">The employee being acted on.</param>
    /// <returns><c>true</c> when the target is the acting user, or the caller is an Admin.</returns>
    public static bool CanWrite(ICurrentUser user, Guid employeeId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsAdmin || user.EmployeeId == employeeId;
    }
}
