using EMS.Application.Common.Models;

namespace EMS.Application.Leave;

/// <summary>Leave balance reads and Admin adjustments.</summary>
/// <remarks>
/// The balance row for the current period is materialised on first access, idempotently, rather
/// than by a scheduled reset. A timer-based reset assumes the application is running on every
/// employee's hire anniversary, which a container that is routinely stopped is not (ADR-0006).
/// </remarks>
public interface ILeaveBalanceService
{
    /// <summary>Reads the acting employee's balances for the current period.</summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One row per leave type that carries a balance.</returns>
    Task<IReadOnlyList<LeaveBalanceDto>> GetOwnBalancesAsync(CancellationToken ct);

    /// <summary>Reads one employee's balances for the current period.</summary>
    /// <param name="employeeId">The employee, subject to the caller's scope.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The balances, or NotFound when out of scope.</returns>
    Task<Result<IReadOnlyList<LeaveBalanceDto>>> GetBalancesAsync(
        Guid employeeId,
        CancellationToken ct);

    /// <summary>Adjusts an entitlement. Admin only.</summary>
    /// <param name="command">The employee, period, new entitlement and mandatory note.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome, written and audited in one transaction.</returns>
    Task<Result> AdjustAsync(AdjustLeaveBalanceCommand command, CancellationToken ct);

    /// <summary>Grants maternity leave, which is never created automatically. Admin only.</summary>
    /// <param name="command">The employee, period, days and mandatory note.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> GrantMaternityAsync(GrantMaternityLeaveCommand command, CancellationToken ct);
}
