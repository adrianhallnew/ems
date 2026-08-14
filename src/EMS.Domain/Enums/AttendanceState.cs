namespace EMS.Domain.Enums;

/// <summary>
/// Attendance state for one employee on one SCT calendar date.
/// </summary>
/// <remarks>
/// Derived at read time and never persisted — this type has no column anywhere in the model,
/// which is the whole point of ADR-0004. Values are listed in resolution order.
/// </remarks>
public enum AttendanceState
{
    /// <summary>Before the hire date, or after deactivation. Excluded from all counts.</summary>
    NotEmployed,

    /// <summary>Saturday or Sunday.</summary>
    Weekend,

    /// <summary>A public holiday.</summary>
    Holiday,

    /// <summary>Within an approved leave request.</summary>
    OnLeave,

    /// <summary>Clocked in at or before 08:00 SCT.</summary>
    Present,

    /// <summary>Clocked in after 08:00 SCT.</summary>
    Late,

    /// <summary>A working day with no attendance record.</summary>
    Absent,
}
