using EMS.Domain.Common;

namespace EMS.Domain.Entities;

/// <summary>
/// One employee's real clock events for one SCT calendar date.
/// </summary>
/// <remarks>
/// Only real events are stored. Absent, Weekend, Holiday, and OnLeave are derived when
/// attendance is read over a date range, so there is no status column here (ADR-0004).
/// </remarks>
public class AttendanceRecord : BaseEntity
{
    /// <summary>Gets or sets the employee this record belongs to.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Gets or sets the employee navigation.</summary>
    public Employee? Employee { get; set; }

    /// <summary>
    /// Gets or sets the SCT calendar date of the clock-in. One record per employee per day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the UTC instant of the clock-in.</summary>
    public DateTime? ClockIn { get; set; }

    /// <summary>Gets or sets the UTC instant of the clock-out.</summary>
    public DateTime? ClockOut { get; set; }

    /// <summary>
    /// Gets or sets whole minutes worked, computed on clock-out or correction.
    /// </summary>
    /// <remarks>
    /// Minutes are what the system measures — two timestamps subtracted. Rendering them as
    /// hours is a presentation concern (ADR-0010).
    /// </remarks>
    public int? WorkedMinutes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a clock-out is missing after the end of the day.
    /// </summary>
    public bool IsFlagged { get; set; }

    /// <summary>Gets or sets the Admin's mandatory reason for a manual adjustment.</summary>
    public string? CorrectionNote { get; set; }

    /// <summary>Gets or sets the Admin who made the correction.</summary>
    public Guid? CorrectedById { get; set; }

    /// <summary>Gets or sets the UTC instant of the correction.</summary>
    public DateTime? CorrectedAt { get; set; }
}
