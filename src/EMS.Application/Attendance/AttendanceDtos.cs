using EMS.Application.Common.Models;
using EMS.Domain.Enums;

namespace EMS.Application.Attendance;

/// <summary>The acting employee's attendance for the current SCT date.</summary>
/// <param name="Date">The SCT calendar date.</param>
/// <param name="State">The resolved state for today.</param>
/// <param name="ClockIn">The clock-in instant in UTC, if recorded.</param>
/// <param name="ClockOut">The clock-out instant in UTC, if recorded.</param>
/// <param name="WorkedMinutes">Whole minutes worked, once clocked out.</param>
/// <param name="CanClockIn">Whether a clock-in is currently permitted.</param>
/// <param name="CanClockOut">Whether a clock-out is currently permitted.</param>
/// <remarks>
/// The two permission flags drive the dashboard button. They are usability, not protection: the
/// unique index on (EmployeeId, Date) is the authoritative guard against a double submit.
/// </remarks>
public sealed record AttendanceTodayDto(
    DateOnly Date,
    AttendanceState State,
    DateTime? ClockIn,
    DateTime? ClockOut,
    int? WorkedMinutes,
    bool CanClockIn,
    bool CanClockOut);

/// <summary>One employee on one date, with the state derived at read time.</summary>
/// <param name="EmployeeId">The employee key.</param>
/// <param name="EmployeeName">The employee's full name.</param>
/// <param name="Date">The SCT calendar date.</param>
/// <param name="State">The resolved state (ADR-0004).</param>
/// <param name="ClockIn">The clock-in instant in UTC, if recorded.</param>
/// <param name="ClockOut">The clock-out instant in UTC, if recorded.</param>
/// <param name="WorkedMinutes">Whole minutes worked, if both events exist.</param>
/// <param name="IsFlagged">Whether the missed-clock-out job flagged this record.</param>
/// <param name="IsEarlyDeparture">
/// Whether the clock-out fell before the end of the working day. Derived for display only; it
/// affects no calculation (spec section 3.3.2).
/// </param>
/// <param name="CorrectionNote">An Admin's reason for a correction, if one was made.</param>
/// <param name="RecordId">The stored record key, or null on a day with no record.</param>
public sealed record AttendanceDayDto(
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    AttendanceState State,
    DateTime? ClockIn,
    DateTime? ClockOut,
    int? WorkedMinutes,
    bool IsFlagged,
    bool IsEarlyDeparture,
    string? CorrectionNote,
    Guid? RecordId);

/// <summary>The filter behind the attendance grids.</summary>
public sealed record AttendanceFilter : PageRequest
{
    /// <summary>Gets the first SCT date in the range, inclusive.</summary>
    public DateOnly From { get; init; }

    /// <summary>Gets the last SCT date in the range, inclusive.</summary>
    public DateOnly To { get; init; }

    /// <summary>Gets the department to restrict to, or null for every department in scope.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>Gets the employee to restrict to, subject to the caller's scope.</summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>Gets the resolved state to restrict to.</summary>
    public AttendanceState? State { get; init; }

    /// <summary>Gets a value indicating whether only flagged records are returned.</summary>
    public bool FlaggedOnly { get; init; }
}

/// <summary>Creates or corrects an attendance record. Admin only.</summary>
/// <param name="RecordId">The record to correct, or null to create one for a date with none.</param>
/// <param name="EmployeeId">The employee the record belongs to.</param>
/// <param name="Date">The SCT calendar date the record belongs to.</param>
/// <param name="ClockIn">The corrected clock-in instant in UTC.</param>
/// <param name="ClockOut">The corrected clock-out instant in UTC.</param>
/// <param name="CorrectionNote">The mandatory reason for the correction.</param>
/// <remarks>
/// A correction may not move a record to a different employee or date; those cases are a delete
/// and a create, both audited (spec section 3.3.6).
/// </remarks>
public sealed record CorrectAttendanceCommand(
    Guid? RecordId,
    Guid EmployeeId,
    DateOnly Date,
    DateTime? ClockIn,
    DateTime? ClockOut,
    string CorrectionNote);
