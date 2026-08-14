using EMS.Domain.Common;
using EMS.Domain.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// A request for leave, and the record of what was decided about it.
/// </summary>
public class LeaveRequest : BaseEntity, ICreatedAtEntity
{
    /// <summary>Gets or sets the requesting employee.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Gets or sets the employee navigation.</summary>
    public Employee? Employee { get; set; }

    /// <summary>Gets or sets the category of leave requested.</summary>
    public LeaveType LeaveType { get; set; }

    /// <summary>Gets or sets the first day of leave, inclusive.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Gets or sets the last day of leave, inclusive.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets the business days the request consumes, fixed at submission.
    /// </summary>
    /// <remarks>
    /// Fixed rather than re-derived, so that deleting a public holiday later cannot
    /// retroactively change what an approved request cost.
    /// </remarks>
    public int BusinessDays { get; set; }

    /// <summary>
    /// Gets or sets the business days returned to the balance on cancellation.
    /// </summary>
    /// <remarks>
    /// Fewer than <see cref="BusinessDays"/> when an Admin cancels mid-leave, since days
    /// already taken are not returned. Without this the audit trail cannot explain a balance
    /// that does not reconcile against request history.
    /// </remarks>
    public int RestoredDays { get; set; }

    /// <summary>Gets or sets the employee's optional reason for the request.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the current status.</summary>
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    /// <summary>Gets or sets the Admin who approved or rejected the request.</summary>
    public Guid? ReviewedById { get; set; }

    /// <summary>Gets or sets the UTC instant of the decision.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Gets or sets the reviewer's optional note.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Gets or sets the UTC instant of cancellation.</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>Gets or sets whoever cancelled the request — the employee or an Admin.</summary>
    public Guid? CancelledById { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token, mapped to SQL Server's rowversion in Phase 2.
    /// </summary>
    /// <remarks>
    /// Maintained by the database on every update, so correctness never depends on the
    /// application remembering to increment it (ADR-0010).
    /// </remarks>
    public byte[] RowVersion { get; set; } = [];
}
