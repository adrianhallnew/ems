using EMS.Application.Common.Models;
using EMS.Domain.Enums;

namespace EMS.Application.Leave;

/// <summary>One row of a leave grid.</summary>
/// <param name="Id">The request key.</param>
/// <param name="EmployeeId">The requesting employee.</param>
/// <param name="EmployeeName">The requesting employee's full name.</param>
/// <param name="LeaveType">The leave type.</param>
/// <param name="StartDate">The first day of leave.</param>
/// <param name="EndDate">The last day of leave.</param>
/// <param name="BusinessDays">The business-day count fixed at submission.</param>
/// <param name="Status">The current status.</param>
/// <param name="CreatedAt">When the request was submitted, in UTC.</param>
public sealed record LeaveRequestListDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    LeaveType LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    int BusinessDays,
    LeaveStatus Status,
    DateTime CreatedAt);

/// <summary>A single leave request in full.</summary>
/// <param name="Summary">The grid-level fields.</param>
/// <param name="Reason">The employee's stated reason, if any.</param>
/// <param name="ReviewedByName">The reviewing Admin's name, once decided.</param>
/// <param name="ReviewedAt">When the decision was made, in UTC.</param>
/// <param name="ReviewNote">The reviewer's note, if any.</param>
/// <param name="CancelledAt">When the request was cancelled, in UTC.</param>
/// <param name="CancelledByName">Who cancelled it.</param>
/// <param name="RestoredDays">
/// How many days a cancellation returned to the balance, which may be fewer than
/// <c>BusinessDays</c> after a mid-leave cancellation (spec section 3.4.5).
/// </param>
/// <param name="CanCancel">Whether the acting user may cancel it now.</param>
/// <param name="CanDecide">
/// Whether the acting user may approve or reject it. False on an Admin's own request, which
/// separation of duties forbids them from deciding.
/// </param>
public sealed record LeaveRequestDetailDto(
    LeaveRequestListDto Summary,
    string? Reason,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewNote,
    DateTime? CancelledAt,
    string? CancelledByName,
    int RestoredDays,
    bool CanCancel,
    bool CanDecide);

/// <summary>One leave balance period.</summary>
/// <param name="LeaveType">The leave type.</param>
/// <param name="PeriodStart">The first day of the period.</param>
/// <param name="PeriodEnd">The last day of the period.</param>
/// <param name="Entitlement">Days granted for the period.</param>
/// <param name="Used">Days consumed by approved leave.</param>
/// <param name="Remaining">Days still available, derived rather than stored.</param>
public sealed record LeaveBalanceDto(
    LeaveType LeaveType,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Entitlement,
    int Used,
    int Remaining);

/// <summary>The filter behind the leave grids.</summary>
public sealed record LeaveFilter : PageRequest
{
    /// <summary>Gets the earliest start date to include.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Gets the latest end date to include.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Gets the department to restrict to, or null for every department in scope.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>Gets the employee to restrict to, subject to the caller's scope.</summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>Gets the leave type to restrict to.</summary>
    public LeaveType? LeaveType { get; init; }

    /// <summary>Gets the status to restrict to.</summary>
    public LeaveStatus? Status { get; init; }
}

/// <summary>Submits a leave request for the acting employee.</summary>
/// <param name="LeaveType">The leave type.</param>
/// <param name="StartDate">The first day of leave.</param>
/// <param name="EndDate">The last day of leave.</param>
/// <param name="Reason">An optional reason.</param>
/// <remarks>
/// No employee identifier: the requester is the authenticated principal, which makes submitting
/// leave on someone else's behalf unrepresentable.
/// </remarks>
public sealed record SubmitLeaveCommand(
    LeaveType LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason);

/// <summary>Approves a pending request.</summary>
/// <param name="RequestId">The request to approve.</param>
/// <param name="Note">An optional note for the employee.</param>
public sealed record ApproveLeaveCommand(Guid RequestId, string? Note);

/// <summary>Rejects a pending request.</summary>
/// <param name="RequestId">The request to reject.</param>
/// <param name="Note">An optional note for the employee.</param>
public sealed record RejectLeaveCommand(Guid RequestId, string? Note);

/// <summary>Cancels a request, restoring balance according to when it is cancelled.</summary>
/// <param name="RequestId">The request to cancel.</param>
/// <param name="Note">An optional note.</param>
public sealed record CancelLeaveCommand(Guid RequestId, string? Note);

/// <summary>Adjusts an employee's balance for one leave type and period. Admin only.</summary>
/// <param name="EmployeeId">The employee whose balance changes.</param>
/// <param name="LeaveType">The leave type.</param>
/// <param name="PeriodStart">The period to adjust, identified by its first day.</param>
/// <param name="Entitlement">The new entitlement in days.</param>
/// <param name="Note">The mandatory reason, audited with the change.</param>
public sealed record AdjustLeaveBalanceCommand(
    Guid EmployeeId,
    LeaveType LeaveType,
    DateOnly PeriodStart,
    int Entitlement,
    string Note);

/// <summary>Grants a maternity balance, which is never created automatically.</summary>
/// <param name="EmployeeId">The employee receiving the grant.</param>
/// <param name="PeriodStart">The first day of the granted period.</param>
/// <param name="PeriodEnd">The last day of the granted period.</param>
/// <param name="Entitlement">The granted days.</param>
/// <param name="Note">The mandatory reason, audited with the grant.</param>
public sealed record GrantMaternityLeaveCommand(
    Guid EmployeeId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Entitlement,
    string Note);
