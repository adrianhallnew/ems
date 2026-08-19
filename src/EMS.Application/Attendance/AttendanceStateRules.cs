using EMS.Domain.Enums;

namespace EMS.Application.Attendance;

/// <summary>The attendance state resolution order, as pure logic.</summary>
/// <remarks>
/// Spec section 3.3.7 fixes the order, and the order is the whole rule: an employee on approved
/// leave during a public holiday resolves to Holiday, and one who never clocked in on a Saturday is
/// not Absent. Splitting this away from the queries is what makes the order testable without a
/// database (ADR-0004).
/// </remarks>
public static class AttendanceStateRules
{
    /// <summary>Resolves one employee's state on one date.</summary>
    /// <param name="date">The SCT calendar date.</param>
    /// <param name="hireDate">The employee's hire date.</param>
    /// <param name="lastEmployedDate">
    /// The last SCT date the employee was employed, or null while they are still active.
    /// </param>
    /// <param name="isPublicHoliday">Whether the date is a public holiday.</param>
    /// <param name="isOnApprovedLeave">Whether an approved leave request covers the date.</param>
    /// <param name="clockInSct">The clock-in time in SCT, or null when no record exists.</param>
    /// <param name="workDayStart">The hour the working day begins, in SCT.</param>
    /// <returns>The resolved state.</returns>
    public static AttendanceState Resolve(
        DateOnly date,
        DateOnly hireDate,
        DateOnly? lastEmployedDate,
        bool isPublicHoliday,
        bool isOnApprovedLeave,
        TimeOnly? clockInSct,
        TimeOnly workDayStart)
    {
        // 1. Outside the employment window: excluded from every count, never counted as absence.
        if (date < hireDate || (lastEmployedDate is { } ended && date > ended))
        {
            return AttendanceState.NotEmployed;
        }

        // 2. Weekend.
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return AttendanceState.Weekend;
        }

        // 3. Public holiday.
        if (isPublicHoliday)
        {
            return AttendanceState.Holiday;
        }

        // 4. Approved leave.
        if (isOnApprovedLeave)
        {
            return AttendanceState.OnLeave;
        }

        // 5 and 6. A record exists: late or present, on the SCT clock-in time.
        if (clockInSct is { } clockIn)
        {
            return clockIn > workDayStart ? AttendanceState.Late : AttendanceState.Present;
        }

        // 7. A working day with no record at all.
        return AttendanceState.Absent;
    }

    /// <summary>Reports whether the clock control may be used on a resolved state.</summary>
    /// <param name="state">The resolved state.</param>
    /// <returns><c>false</c> on Weekend, Holiday, OnLeave and NotEmployed.</returns>
    public static bool AllowsClocking(AttendanceState state) =>
        state is AttendanceState.Present or AttendanceState.Late or AttendanceState.Absent;

    /// <summary>Reports whether a clock-out counts as an early departure.</summary>
    /// <param name="clockOutSct">The clock-out time in SCT, or null.</param>
    /// <param name="workDayEnd">The hour the working day ends, in SCT.</param>
    /// <returns><c>true</c> when the employee left before the end of the day.</returns>
    /// <remarks>
    /// A display flag only. It is not stored and affects no calculation (spec section 3.3.2).
    /// </remarks>
    public static bool IsEarlyDeparture(TimeOnly? clockOutSct, TimeOnly workDayEnd) =>
        clockOutSct is { } clockOut && clockOut < workDayEnd;
}
