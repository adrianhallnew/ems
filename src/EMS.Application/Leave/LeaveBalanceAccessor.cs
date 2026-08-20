using EMS.Application.Common.Interfaces;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EMS.Application.Leave;

/// <summary>
/// Materialises an employee's balance period on first access.
/// </summary>
/// <remarks>
/// There is no scheduled reset. The application runs in a container that is routinely stopped, so a
/// timer-based reset would silently skip the anniversary of anyone whose reset date fell during
/// downtime; lazy materialisation is correct whenever the balance is next read (spec §3.4.2,
/// ADR-0006).
/// <para>
/// Every method takes the caller's context rather than creating one, so materialisation happens
/// inside whatever transaction the caller has open. A balance created on a second context would
/// survive a rolled-back submission.
/// </para>
/// </remarks>
internal static class LeaveBalanceAccessor
{
    /// <summary>Leave types that carry a balance row and reset each period.</summary>
    /// <remarks>
    /// Maternity is absent because an Admin grants it explicitly with its own period, and Unpaid is
    /// absent because it has no cap and is always available (spec §3.4.1, §3.4.2).
    /// </remarks>
    private static readonly LeaveType[] AutoCreated =
    [
        LeaveType.Annual,
        LeaveType.Sick,
        LeaveType.Compassionate,
    ];

    /// <summary>
    /// Ensures the employee's current-period rows exist and returns every balance they hold.
    /// </summary>
    /// <param name="db">The caller's context, inside the caller's transaction.</param>
    /// <param name="employee">The employee, already loaded and tracked.</param>
    /// <param name="today">The current SCT date.</param>
    /// <param name="entitlements">Default days per leave type, from configuration.</param>
    /// <param name="ct">Cancels the queries.</param>
    /// <returns>Every balance row the employee holds, current period first.</returns>
    public static async Task<IReadOnlyList<LeaveBalance>> EnsureCurrentPeriodAsync(
        IApplicationDbContext db,
        Employee employee,
        DateOnly today,
        IReadOnlyDictionary<LeaveType, int> entitlements,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(entitlements);

        var existing = await db.LeaveBalances
            .Where(b => b.EmployeeId == employee.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Before the hire date there is no period to materialise, which PeriodFor would throw over.
        if (today < employee.HireDate)
        {
            return existing;
        }

        var (start, end) = employee.PeriodFor(today);

        foreach (var leaveType in AutoCreated)
        {
            var present = existing.Exists(b => b.LeaveType == leaveType && b.PeriodStart == start);

            if (present)
            {
                continue;
            }

            var balance = new LeaveBalance
            {
                EmployeeId = employee.Id,
                LeaveType = leaveType,
                PeriodStart = start,
                PeriodEnd = end,
                Entitlement = entitlements.TryGetValue(leaveType, out var days) ? days : 0,
                Used = 0,
            };

            db.LeaveBalances.Add(balance);
            existing.Add(balance);
        }

        return existing;
    }

    /// <summary>
    /// Returns the balance a request draws on, or null when the type needs none.
    /// </summary>
    /// <param name="balances">The employee's balances, already materialised.</param>
    /// <param name="leaveType">The requested leave type.</param>
    /// <param name="startDate">The first day of the request, which selects the period.</param>
    /// <returns>The matching balance row, or null for Unpaid and for a period never granted.</returns>
    /// <remarks>
    /// Unpaid deducts from nothing (spec §3.4.1), so a null here is an outcome rather than an error
    /// for that type. For Maternity it means no Admin has granted a period covering the request.
    /// </remarks>
    public static LeaveBalance? For(
        IReadOnlyList<LeaveBalance> balances,
        LeaveType leaveType,
        DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(balances);

        if (leaveType == LeaveType.Unpaid)
        {
            return null;
        }

        return balances.FirstOrDefault(b =>
            b.LeaveType == leaveType
            && b.PeriodStart <= startDate
            && b.PeriodEnd >= startDate);
    }
}
