using EMS.Domain.Common;
using EMS.Domain.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// One employee's entitlement and usage for one leave type over one balance period.
/// </summary>
/// <remarks>
/// The row for the current period is created on first access, idempotently. There is no
/// scheduled reset job, because the application is not continuously running (ADR-0006).
/// </remarks>
public class LeaveBalance : BaseEntity
{
    /// <summary>Gets or sets the employee this balance belongs to.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Gets or sets the employee navigation.</summary>
    public Employee? Employee { get; set; }

    /// <summary>Gets or sets the leave type this balance covers.</summary>
    public LeaveType LeaveType { get; set; }

    /// <summary>Gets or sets the first day of the period — a hire anniversary.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Gets or sets the last day of the period — the day before the next anniversary.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Gets or sets the days granted for this period. No carry-over.</summary>
    public int Entitlement { get; set; }

    /// <summary>Gets or sets the days consumed by approved leave in this period.</summary>
    public int Used { get; set; }

    /// <summary>
    /// Gets or sets the reason an Admin last adjusted this balance, or null if never adjusted.
    /// </summary>
    /// <remarks>
    /// Spec §3.4.7 makes the note mandatory on every adjustment and requires it written in the same
    /// transaction as the balance change. The column holds only the most recent one; the audit
    /// interceptor records the before and after of every change to it, so the history is in the
    /// audit trail rather than here.
    /// </remarks>
    public string? LastAdjustmentNote { get; set; }

    /// <summary>
    /// Gets the days still available.
    /// </summary>
    /// <remarks>
    /// A computed property with no setter and no backing field, so it is not a column.
    /// </remarks>
    public int Remaining => Entitlement - Used;

    /// <summary>
    /// Gets or sets the concurrency token, mapped to SQL Server's rowversion in Phase 2.
    /// </summary>
    /// <remarks>
    /// This is what stops two Admins approving simultaneously from both reading the same
    /// starting balance and overdrawing it.
    /// </remarks>
    public byte[] RowVersion { get; set; } = [];
}
