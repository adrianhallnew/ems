namespace EMS.Application.Attendance;

/// <summary>
/// Derives attendance states for a set of employees over a date range.
/// </summary>
/// <remarks>
/// The replacement for a stored status column (ADR-0004). Every screen that reads attendance —
/// dashboard, records grid, monthly report — goes through this one component, so the rules cannot
/// drift between them.
/// </remarks>
public interface IAttendanceStateResolver
{
    /// <summary>Projects one row per employee per date in the range.</summary>
    /// <param name="employeeIds">The employees to resolve, already narrowed to the caller's scope.</param>
    /// <param name="startDate">The first SCT date, inclusive.</param>
    /// <param name="endDate">The last SCT date, inclusive.</param>
    /// <param name="ct">Cancels the queries.</param>
    /// <returns>
    /// One row per employee per date, excluding dates that resolve to NotEmployed. Assembled from
    /// three indexed queries: attendance records, public holidays, and approved leave.
    /// </returns>
    Task<IReadOnlyList<AttendanceDayDto>> ResolveAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct);
}
